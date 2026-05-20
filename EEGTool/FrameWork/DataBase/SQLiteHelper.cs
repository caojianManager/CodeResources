using FrameWork.Log;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EegAcquisitionSystem.FrameWork.DataBase
{
    public class SQLiteHelper
    {
        private string _dbResourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Model","DB","Database.db");
        private string _targetDBResourcesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EegAcquisitionSystem");

        private static SQLiteHelper _instance = new SQLiteHelper();
        public static SQLiteHelper Instance { get { return _instance; } }

        private string _connStr { get; set; } = string.Empty;

        public SQLiteHelper() 
        { 
          
        }

        public void Init()
        {
            try
            {
                if (!Directory.Exists(_targetDBResourcesPath))
                {
                    Directory.CreateDirectory(_targetDBResourcesPath);
                }

                var dbfile = Path.Combine(_targetDBResourcesPath, "eeg_acquisition.db");

                // 如果目标数据库不存在，直接拷贝模板数据库
                if (!File.Exists(dbfile))
                {
                    File.Copy(_dbResourcesPath, dbfile, overwrite: false);
                }

                _connStr = $"Data Source={dbfile};";
            }
            catch (Exception ex)
            {
                Logger.Debug($"[SQLiteHelper]: 数据库初始化失败 {ex}");
            }
        }


        public void DeleteLocalDB()
        {
            var dbfile = Path.Combine(_targetDBResourcesPath, "eeg_acquisition.db");
            if (!File.Exists(dbfile))
            {
                File.Delete(dbfile);    
            }
        }

        #region === 数据库操作 ===

        private SQLiteConnection GetConnection()
        {
            if (string.IsNullOrWhiteSpace(_connStr))
            {
                Init();
            }
            return new SQLiteConnection(_connStr);
        }

        // === 执行非查询（INSERT / UPDATE / DELETE）===

        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SQLiteCommand(sql, conn);

            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteNonQuery();
        }
        // === 插入数据后，返回自增的行数ID ===
        public int ExecuteInsertReturnId(string sql, params SQLiteParameter[] parameters)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = new SQLiteCommand(sql, conn);

                // 添加参数（复用原有参数处理逻辑）
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                conn.Open();
                // 执行插入（先确保数据插入成功）
                int affectedRows = cmd.ExecuteNonQuery();
                if (affectedRows <= 0)
                {
                    Logger.Debug("[SQLiteHelper]: 插入数据失败，受影响行数为0");
                    return 0;
                }

                // 执行查询获取自增ID（SQLite特有，同一个连接内有效）
                cmd.CommandText = "SELECT last_insert_rowid()";
                var result = cmd.ExecuteScalar();
                return result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[SQLiteHelper]: 执行插入并获取自增ID失败 {ex}");
                return 0;
            }
        }
        // === 查询单个值 ===

        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SQLiteCommand(sql, conn);

            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteScalar();
        }

        // === 查询 DataTable ===

        public DataTable Query(string sql, params SQLiteParameter[] parameters)
        {
            using var conn = GetConnection();
            using var cmd = new SQLiteCommand(sql, conn);

            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            using var adapter = new SQLiteDataAdapter(cmd);
            var table = new DataTable();
            adapter.Fill(table);

            return table;
        }

        // === 查询 List<T> ===

        public List<T> QueryList<T>(string sql, Func<SQLiteDataReader, T> map,
            params SQLiteParameter[] parameters)
        {
            var list = new List<T>();

            using var conn = GetConnection();
            using var cmd = new SQLiteCommand(sql, conn);

            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(map(reader));
            }

            return list;
        }

       // === 事务 ===

        public void ExecuteTransaction(Action<SQLiteConnection, SQLiteTransaction> action)
        {
            using var conn = GetConnection();
            conn.Open();

            using var tran = conn.BeginTransaction();
            try
            {
                action(conn, tran);
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        #endregion
    }
}
