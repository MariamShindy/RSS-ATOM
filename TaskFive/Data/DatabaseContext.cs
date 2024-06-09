using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace TaskFive.Data
{
	public class DatabaseContext
	{
		private const string connectionString = "Data source=rss_atom_reader.db";

		public static void CreateTables()
		{
			using var connection = new SqliteConnection(connectionString);
			connection.Execute(@"
            CREATE TABLE IF NOT EXISTS USERS(
              ID INTEGER PRIMARY KEY AUTOINCREMENT,
              EMAIL TEXT NOT NULL,
              PASSWORD TEXT NOT NULL
            );
             CREATE TABLE IF NOT EXISTS FEEDS(
              ID INTEGER PRIMARY KEY AUTOINCREMENT,
              USERID INTEGER,
              URL TEXT NOT NULL,
              FOREIGN KEY(USERID) REFERENCES USERS(ID)
            );"
            );
		}
		public static IDbConnection GetConnection() => new SqliteConnection(connectionString);

	}
}
