﻿/********************************************************
 * ADO.NET 2.0 Data Provider for SQLite Version 3.X
 * Written by Robert Simpson (robert@blackcastlesoft.com)
 * 
 * Released to the public domain, use at your own risk!
 ********************************************************/

namespace System.Data.SQLite
{
  using System;
  using System.Data;
  using System.Data.Common;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Reflection;

  /// <summary>
  /// SQLite implementation of DbDataReader.
  /// </summary>
  public sealed class SQLiteDataReader : DbDataReader
  {
    /// <summary>
    /// Underlying command this reader is attached to
    /// </summary>
    private SQLiteCommand   _command;
    /// <summary>
    /// Index of the current statement in the command being processed
    /// </summary>
    private int             _activeStatementIndex;
    /// <summary>
    /// Current statement being Read()
    /// </summary>
    private SQLiteStatement _activeStatement;
    /// <summary>
    /// State of the current statement being processed.
    /// -1 = First Step() executed, so the first Read() will be ignored
    ///  0 = Actively reading
    ///  1 = Finished reading
    ///  2 = Non-row-returning statement, no records
    /// </summary>
    private int             _readingState;
    /// <summary>
    /// Number of records affected by the insert/update statements executed on the command
    /// </summary>
    private int             _rowsAffected;
    /// <summary>
    /// Count of fields (columns) in the row-returning statement currently being processed
    /// </summary>
    private int             _fieldCount;
    /// <summary>
    /// Datatypes of active fields (columns) in the current statement, used for type-restricting data
    /// </summary>
    private SQLiteType[]    _fieldTypeArray;

    /// <summary>
    /// The behavior of the datareader
    /// </summary>
    private CommandBehavior _commandBehavior;

    /// <summary>
    /// Internal constructor, initializes the datareader and sets up to begin executing statements
    /// </summary>
    /// <param name="cmd">The SQLiteCommand this data reader is for</param>
    /// <param name="behave">The expected behavior of the data reader</param>
    internal SQLiteDataReader(SQLiteCommand cmd, CommandBehavior behave)
    {
      _command = cmd;
      _commandBehavior = behave;
      Initialize();

      if (_command != null)
        NextResult();
    }

    /// <summary>
    /// Initializes and resets the datareader's member variables
    /// </summary>
    internal void Initialize()
    {
      _activeStatementIndex = -1;
      _activeStatement = null;
      _rowsAffected = 0;
      _fieldCount = -1;
    }

    /// <summary>
    /// Closes the datareader, potentially closing the connection as well if CommandBehavior.CloseConnection was specified.
    /// </summary>
    public override void Close()
    {
      if (_command != null)
      {
        while (NextResult())
        {
        }
        _command.ClearDataReader();
      }

      // If the datareader's behavior includes closing the connection, then do so here.
      if ((_commandBehavior & CommandBehavior.CloseConnection) != 0)
        _command.Connection.Close();

      _command = null;
      _activeStatement = null;
      _fieldTypeArray = null;
    }

    /// <summary>
    /// Disposes the datareader.  Calls Close() to ensure everything is cleaned up.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
      Close();
      base.Dispose(disposing);
    }

    /// <summary>
    /// Throw an error if the datareader is closed
    /// </summary>
    private void CheckClosed()
    {
      if (_command == null)
        throw new InvalidOperationException("DataReader has been closed");
    }

    /// <summary>
    /// Throw an error if a row is not loaded
    /// </summary>
    private void CheckValidRow()
    {
      if (_readingState != 0)
        throw new InvalidOperationException("No current row");
    }

    /// <summary>
    /// Enumerator support
    /// </summary>
    /// <returns>Returns a DbEnumerator object.</returns>
    public override Collections.IEnumerator GetEnumerator()
    {
      return new DbEnumerator(this);
    }

    /// <summary>
    /// Not implemented.  Returns 0
    /// </summary>
    public override int Depth
    {
      get
      {
        CheckClosed();
        return 0;
      }
    }

    /// <summary>
    /// Returns the number of columns in the current resultset
    /// </summary>
    public override int FieldCount
    {
      get
      {
        CheckClosed();
        return _fieldCount;
      }
    }

    /// <summary>
    /// SQLite is inherently un-typed.  All datatypes in SQLite are natively strings.  The definition of the columns of a table
    /// and the affinity of returned types are all we have to go on to type-restrict data in the reader.
    /// 
    /// This function attempts to verify that the type of data being requested of a column matches the datatype of the column.  In
    /// the case of columns that are not backed into a table definition, we attempt to match up the affinity of a column (int, double, string or blob)
    /// to a set of known types that closely match that affinity.  It's not an exact science, but its the best we can do.
    /// </summary>
    /// <returns>
    /// This function throws an InvalidTypeCast() exception if the requested type doesn't match the column's definition or affinity.
    /// </returns>
    /// <param name="i">The index of the column to type-check</param>
    /// <param name="typ">The type we want to get out of the column</param>
    private void VerifyType(int i, DbType typ)
    {
      CheckValidRow();
      SQLiteType t = GetSQLiteType(i);

      if (t.Type == typ) return;

        // Coercable type, usually a literal of some kind
      switch (_fieldTypeArray[i].Affinity)
      {
        case TypeAffinity.Int64:
          if (typ == DbType.Int16) return;
          if (typ == DbType.Int32) return;
          if (typ == DbType.Int64) return;
          if (typ == DbType.Boolean) return;
          if (typ == DbType.Byte) return;
          break;
        case TypeAffinity.Double:
          if (typ == DbType.Single) return;
          if (typ == DbType.Double) return;
          if (typ == DbType.Decimal) return;
          break;
        case TypeAffinity.Text:
          if (typ == DbType.SByte) return;
          if (typ == DbType.String) return;
          if (typ == DbType.SByte) return;
          if (typ == DbType.Guid) return;
          if (typ == DbType.DateTime) return;
          break;
        case TypeAffinity.Blob:
          if (typ == DbType.String) return;
          if (typ == DbType.Binary) return;
          break;
      }

      throw new InvalidCastException();
    }

    /// <summary>
    /// Retrieves the column as a boolean value
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>bool</returns>
    public override bool GetBoolean(int i)
    {
      VerifyType(i, DbType.Boolean);
      return Convert.ToBoolean(GetValue(i), CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Retrieves the column as a single byte value
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>byte</returns>
    public override byte GetByte(int i)
    {
      VerifyType(i, DbType.Byte);
      return Convert.ToByte(_activeStatement._sql.GetInt32(_activeStatement, i));
    }

    /// <summary>
    /// Retrieves a column as an array of bytes (blob)
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <param name="fieldOffset">The zero-based index of where to begin reading the data</param>
    /// <param name="buffer">The buffer to write the bytes into</param>
    /// <param name="bufferoffset">The zero-based index of where to begin writing into the array</param>
    /// <param name="length">The number of bytes to retrieve</param>
    /// <returns>The actual number of bytes written into the array</returns>
    /// <remarks>
    /// To determine the number of bytes in the column, pass a null value for the buffer.  The total length will be returned.
    /// </remarks>
    public override long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
    {
      VerifyType(i, DbType.Binary);
      return _activeStatement._sql.GetBytes(_activeStatement, i, (int)fieldOffset, buffer, bufferoffset, length);
    }

    /// <summary>
    /// Returns the column as a single character
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>char</returns>
    public override char GetChar(int i)
    {
      VerifyType(i, DbType.SByte);
      return Convert.ToChar(_activeStatement._sql.GetInt32(_activeStatement, i));
    }

    /// <summary>
    /// Retrieves a column as an array of chars (blob)
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <param name="fieldoffset">The zero-based index of where to begin reading the data</param>
    /// <param name="buffer">The buffer to write the characters into</param>
    /// <param name="bufferoffset">The zero-based index of where to begin writing into the array</param>
    /// <param name="length">The number of bytes to retrieve</param>
    /// <returns>The actual number of characters written into the array</returns>
    /// <remarks>
    /// To determine the number of characters in the column, pass a null value for the buffer.  The total length will be returned.
    /// </remarks>
    public override long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
    {
      VerifyType(i, DbType.String);
      return _activeStatement._sql.GetChars(_activeStatement, i, (int)fieldoffset, buffer, bufferoffset, length);
    }

    /// <summary>
    /// Retrieves the name of the back-end datatype of the column
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>string</returns>
    public override string GetDataTypeName(int i)
    {
      CheckClosed();
      SQLiteType typ = GetSQLiteType(i);

      if (typ.Type == DbType.Object) return SQLiteConvert.SQLiteTypeToType(typ).Name;

      return _activeStatement._sql.ColumnType(_activeStatement, i, out typ.Affinity);
    }

    /// <summary>
    /// Retrieve the column as a date/time value
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>DateTime</returns>
    public override DateTime GetDateTime(int i)
    {
      VerifyType(i, DbType.DateTime);
      return _activeStatement._sql.GetDateTime(_activeStatement, i);
    }

    /// <summary>
    /// Retrieve the column as a decimal value
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>decimal</returns>
    public override decimal GetDecimal(int i)
    {
      VerifyType(i, DbType.Decimal);
      return Convert.ToDecimal(_activeStatement._sql.GetDouble(_activeStatement, i));
    }

    /// <summary>
    /// Returns the column as a double
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>double</returns>
    public override double GetDouble(int i)
    {
      VerifyType(i, DbType.Double);
      return _activeStatement._sql.GetDouble(_activeStatement, i);
    }

    /// <summary>
    /// Returns the .NET type of a given column
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>Type</returns>
    public override Type GetFieldType(int i)
    {
      return SQLiteConvert.SQLiteTypeToType(GetSQLiteType(i));
    }

    /// <summary>
    /// Returns a column as a float value
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>float</returns>
    public override float GetFloat(int i)
    {
      VerifyType(i, DbType.Single);
      return Convert.ToSingle(_activeStatement._sql.GetDouble(_activeStatement, i));
    }

    /// <summary>
    /// Returns the column as a Guid
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>Guid</returns>
    public override Guid GetGuid(int i)
    {
      VerifyType(i, DbType.Guid);
      return new Guid(_activeStatement._sql.GetText(_activeStatement, i));
    }

    /// <summary>
    /// Returns the column as a short
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>Int16</returns>
    public override Int16 GetInt16(int i)
    {
      VerifyType(i, DbType.Int16);
      return Convert.ToInt16(_activeStatement._sql.GetInt32(_activeStatement, i));
    }

    /// <summary>
    /// Retrieves the column as an int
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>Int32</returns>
    public override Int32 GetInt32(int i)
    {
      VerifyType(i, DbType.Int32);
      return _activeStatement._sql.GetInt32(_activeStatement, i);
    }

    /// <summary>
    /// Retrieves the column as a long
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>Int64</returns>
    public override Int64 GetInt64(int i)
    {
      VerifyType(i, DbType.Int64);
      return _activeStatement._sql.GetInt64(_activeStatement, i);
    }

    /// <summary>
    /// Retrieves the name of the column
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>string</returns>
    public override string GetName(int i)
    {
      CheckClosed();
      return _activeStatement._sql.ColumnName(_activeStatement, i);
    }

    /// <summary>
    /// Retrieves the i of a column, given its name
    /// </summary>
    /// <param name="name">The name of the column to retrieve</param>
    /// <returns>The int i of the column</returns>
    public override int GetOrdinal(string name)
    {
      CheckClosed();
      return _activeStatement._sql.ColumnIndex(_activeStatement, name);
    }

    /// <summary>
    /// Schema information in SQLite is an iffy-business.  We've extended the native SQLite3.DLL to include a special pragma called
    /// PRAGMA real_column_names
    /// When enabled, the pragma causes all column aliases to be ignored, and the full Database.Table.ColumnName to be returned for
    /// each column of a SELECT statement.  Using this information it is then possible to query each database and table for the
    /// matching column, and associate it with the active statement.
    /// </summary>
    /// <remarks>
    /// The current connection is cloned for the sake of executing this statement, so as to avoid any possibility of corrupting the
    /// original connection's existing statements or state.  Any attached databases are re-attached to the new connection.
    /// </remarks>
    /// <returns>Returns a DataTable containing the schema information for the active SELECT statement being processed.</returns>
    public override DataTable GetSchemaTable()
    {
      CheckClosed();

      DataTable tbl = new DataTable("SchemaTable");
      string[] arName;
      string strTable;
      string strCatalog;
      DataRow row;

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add(SchemaTableColumn.ColumnName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
      tbl.Columns.Add(SchemaTableColumn.ColumnSize, typeof(int));
      tbl.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(short));
      tbl.Columns.Add(SchemaTableColumn.NumericScale, typeof(short));
      tbl.Columns.Add(SchemaTableColumn.IsUnique, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.IsKey, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.BaseServerName, typeof(string));
      tbl.Columns.Add(SchemaTableOptionalColumn.BaseCatalogName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.BaseColumnName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.BaseSchemaName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.BaseTableName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
      tbl.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
      tbl.Columns.Add(SchemaTableColumn.IsAliased, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.IsExpression, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsAutoIncrement, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsRowVersion, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsHidden, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.IsLong, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsReadOnly, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.ProviderSpecificDataType, typeof(Type));
      tbl.Columns.Add(SchemaTableOptionalColumn.DefaultValue, typeof(object));

      tbl.BeginLoadData();

      SQLiteConnection cnn = (SQLiteConnection)_command.Connection;

      try
      {
        cnn._sql.SetRealColNames(true);

        // Create a new command based on the original.  The only difference being that this new command returns
        // fully-qualified Database.Table.Column column names because of the above pragma
        using (SQLiteCommand cmd = (SQLiteCommand)_command.Clone())
        {
          using (DbDataReader rd = cmd.ExecuteReader(CommandBehavior.SchemaOnly))
          {
            // No need to Read() from this reader, we just want the column names
            for (int n = 0; n < _fieldCount; n++)
            {
              strTable = "";
              strCatalog = "main";

              row = tbl.NewRow();

              // Default settings for the column
              row[SchemaTableColumn.ColumnName] = GetName(n);
              row[SchemaTableColumn.ColumnOrdinal] = n;
              row[SchemaTableColumn.ColumnSize] = 0;
              row[SchemaTableColumn.NumericPrecision] = 0;
              row[SchemaTableColumn.NumericScale] = 0;
              row[SchemaTableColumn.ProviderType] = GetSQLiteType(n).Type;
              row[SchemaTableColumn.IsLong] = false;
              row[SchemaTableColumn.AllowDBNull] = true;
              row[SchemaTableOptionalColumn.IsReadOnly] = false;
              row[SchemaTableOptionalColumn.IsRowVersion] = false;
              row[SchemaTableColumn.IsUnique] = false;
              row[SchemaTableColumn.IsKey] = false;
              row[SchemaTableOptionalColumn.IsAutoIncrement] = false;
              row[SchemaTableOptionalColumn.IsReadOnly] = false;
              row[SchemaTableColumn.BaseColumnName] = GetName(n);

              // Try and extract the database, table and column from the datareader
              arName = rd.GetName(n).Split('.');

              if (arName.Length > 1)
                strTable = arName[arName.Length - 2];

              if (arName.Length > 2)
                strCatalog = arName[arName.Length - 3];

              // If we have a table-bound column, extract the extra information from it
              if (arName.Length > 1)
              {
                using (SQLiteCommand cmdTable = new SQLiteCommand(String.Format(CultureInfo.InvariantCulture, "PRAGMA [{1}].TABLE_INFO([{0}])", strTable, strCatalog), cnn))
                {
                  if (arName.Length < 3) strCatalog = "main";

                  using (DbDataReader rdTable = cmdTable.ExecuteReader())
                  {
                    while (rdTable.Read())
                    {
                      if (String.Compare(arName[arName.Length - 1], rdTable.GetString(1), true, CultureInfo.InvariantCulture) == 0)
                      {
                        string strType = rdTable.GetString(2);
                        string[] arSize = strType.Split('(');
                        if (arSize.Length > 1)
                        {
                          strType = arSize[0];
                          arSize = arSize[1].Split(')');
                          if (arSize.Length > 1)
                            row[SchemaTableColumn.ColumnSize] = Convert.ToInt32(arSize[0], CultureInfo.InvariantCulture);
                        }

                        bool bNotNull = rdTable.GetBoolean(3);
                        bool bPrimaryKey = rdTable.GetBoolean(5);

                        row[SchemaTableColumn.DataType] = GetFieldType(n);
                        row[SchemaTableColumn.BaseTableName] = strTable;
                        row[SchemaTableColumn.BaseColumnName] = rdTable.GetString(1);
                        if (String.IsNullOrEmpty(strCatalog) == false)
                        {
                          row[SchemaTableOptionalColumn.BaseCatalogName] = strCatalog;
                        }

                        row[SchemaTableColumn.AllowDBNull] = (!bNotNull && !bPrimaryKey);
                        row[SchemaTableColumn.IsUnique] = bPrimaryKey;
                        row[SchemaTableColumn.IsKey] = bPrimaryKey;
                        row[SchemaTableOptionalColumn.IsAutoIncrement] = bPrimaryKey;
                        if (rdTable.IsDBNull(4) == false)
                          row[SchemaTableOptionalColumn.DefaultValue] = rdTable[4];
                        break;
                      }
                    }
                  }
                }
              }
              tbl.Rows.Add(row);
            }
          }
        }
      }
      finally
      {
        cnn._sql.SetRealColNames(false);
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Retrieves the column as a string
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>string</returns>
    public override string GetString(int i)
    {
      VerifyType(i, DbType.String);
      return _activeStatement._sql.GetText(_activeStatement, i);
    }

    /// <summary>
    /// Retrieves the column as an object corresponding to the underlying datatype of the column
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>object</returns>
    public override object GetValue(int i)
    {
      SQLiteType typ = GetSQLiteType(i);

      return _activeStatement._sql.GetValue(_activeStatement, i, ref typ);
    }

    /// <summary>
    /// Retreives the values of multiple columns, up to the size of the supplied array
    /// </summary>
    /// <param name="values">The array to fill with values from the columns in the current resultset</param>
    /// <returns>The number of columns retrieved</returns>
    public override int GetValues(object[] values)
    {
      int nMax = _fieldCount;
      if (values.Length < nMax) nMax = values.Length;

      for (int n = 0; n < nMax; n++)
      {
        values.SetValue(GetValue(n), n);
      }

      return nMax;
    }

    /// <summary>
    /// Returns True if the resultset has rows that can be fetched
    /// </summary>
    public override bool HasRows
    {
      get
      {
        CheckClosed();
        return (_readingState != 1);
      }
    }

    /// <summary>
    /// Returns True if the data reader is closed
    /// </summary>
    public override bool IsClosed
    {
      get { return (_command == null); }
    }

    /// <summary>
    /// Returns True if the specified column is null
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>True or False</returns>
    public override bool IsDBNull(int i)
    {
      CheckClosed();
      return _activeStatement._sql.IsNull(_activeStatement, i);
    }

    /// <summary>
    /// Moves to the next resultset in multiple row-returning SQL command.
    /// </summary>
    /// <returns>True if the command was successful and a new resultset is available, False otherwise.</returns>
    public override bool NextResult()
    {
      CheckClosed();

      SQLiteStatement stmt = null;
      int fieldCount;

      while (true)
      {
        if (_activeStatement != null && stmt == null)
        {
          // If we're only supposed to return a single rowset, step through all remaining statements once until
          // they are all done and return false to indicate no more resultsets exist.
          if ((_commandBehavior & CommandBehavior.SingleResult) != 0)
          {
            // Reset the previously-executed command
            _activeStatement._sql.Reset(_activeStatement);

            for (; ; )
            {
              stmt = _command.GetStatement(_activeStatementIndex);
              _activeStatementIndex++;
              if (stmt == null) break;

              stmt._sql.Step(stmt);
              _rowsAffected += stmt._sql.Changes;
              stmt._sql.Reset(stmt); // Gotta reset after every step to release any locks and such!
            }
            return false;
          }
          else
          {
            // Reset the previously-executed command
            _activeStatement._sql.Reset(_activeStatement);
          }
        }

        // Get the next statement to execute
        stmt = _command.GetStatement(_activeStatementIndex + 1);

        // If we've reached the end of the statements, return false, no more resultsets
        if (stmt == null)
          return false;

        // If we were on a current resultset, set the state to "done reading" for it
        if (_readingState < 1)
          _readingState = 1;

        _activeStatementIndex++;

        fieldCount = stmt._sql.ColumnCount(stmt);

        // If we're told to get schema information only, then don't perform an initial step() through the resultset
        if ((_commandBehavior & CommandBehavior.SchemaOnly) == 0 || fieldCount == 0)
        {
          if (stmt._sql.Step(stmt))
          {
            _readingState = -1;
          }
          else if (fieldCount == 0) // No rows returned, if fieldCount is zero, skip to the next statement
          {
            _rowsAffected += stmt._sql.Changes;
            stmt._sql.Reset(stmt);
            continue; // Skip this command and move to the next, it was not a row-returning resultset
          }
          else // No rows, fieldCount is non-zero so stop here
          {
            _readingState = 1; // This command returned columns but no rows, so return true, but HasRows = false and Read() returns false
          }
        }

        // Ahh, we found a row-returning resultset eligible to be returned!
        _activeStatement = stmt;
        _fieldCount = fieldCount;
        _fieldTypeArray = null;

        return true;
      }
    }

    /// <summary>
    /// Retrieves the SQLiteType for a given column, and caches it to avoid repetetive interop calls.
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>A SQLiteType structure</returns>
    private SQLiteType GetSQLiteType(int i)
    {
      CheckClosed();
      if (_fieldTypeArray == null) _fieldTypeArray = new SQLiteType[_fieldCount];

      if (_fieldTypeArray[i].Affinity == TypeAffinity.Uninitialized || _fieldTypeArray[i].Affinity == TypeAffinity.Null)
        _fieldTypeArray[i].Type = SQLiteConvert.TypeNameToDbType(_activeStatement._sql.ColumnType(_activeStatement, i, out _fieldTypeArray[i].Affinity));
      return _fieldTypeArray[i];
    }

    /// <summary>
    /// Reads the next row from the resultset
    /// </summary>
    /// <returns>True if a new row was successfully loaded and is ready for processing</returns>
    public override bool Read()
    {
      CheckClosed();

      if (_readingState == -1) // First step was already done at the NextResult() level, so don't step again, just return true.
      {
        _readingState = 0;
        return true;
      }
      else if (_readingState == 0) // Actively reading rows
      {
        if (_activeStatement._sql.Step(_activeStatement) == true)
          return true;

        _readingState = 1; // Finished reading rows
      }

      return false;
    }

    /// <summary>
    /// Retrieve the count of records affected by an update/insert command.  Only valid once the data reader is closed!
    /// </summary>
    public override int RecordsAffected
    {
      get { return (IsClosed) ? _rowsAffected : -1; }
    }

    /// <summary>
    /// Indexer to retrieve data from a column given its name
    /// </summary>
    /// <param name="name">The name of the column to retrieve data for</param>
    /// <returns>The value contained in the column</returns>
    public override object this[string name]
    {
      get { return GetValue(GetOrdinal(name)); }
    }

    /// <summary>
    /// Indexer to retrieve data from a column given its i
    /// </summary>
    /// <param name="i">The index of the column to retrieve</param>
    /// <returns>The value contained in the column</returns>
    public override object this[int i]
    {
      get { return GetValue(i); }
    }
  }
}
