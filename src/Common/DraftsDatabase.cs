using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Vintagestory.API.Common;

namespace PowerOfMind.Drafts
{
	internal class DraftsDatabase
	{
		private readonly ILogger logger;
		private readonly SqliteConnection conn;

		public DraftsDatabase(IWorldAccessor accessor, ILogger logger)
		{
			this.logger = logger;
			conn = (SqliteConnection)GetField(GetField(GetField(GetField(accessor, "chunkThread"), "gameDatabase"), "conn"), "sqliteConn");
		}

		public void Init()
		{
			using(SqliteCommand cmd = conn.CreateCommand())
			{
				cmd.CommandText = "CREATE TABLE IF NOT EXISTS pom_draftsmen (player TEXT, code_domain TEXT, code_path TEXT, PRIMARY KEY (player, code_domain, code_path));";
				cmd.ExecuteNonQuery();
			}
		}

		public int Remap(AssetLocation fromCode, AssetLocation toCode)
		{
			using var transaction = conn.BeginTransaction();
			using(SqliteCommand cmd = conn.CreateCommand())
			{
				cmd.CommandText = "INSERT OR IGNORE INTO pom_draftsmen (player,code_domain,code_path) SELECT player,@to_domain,@to_path FROM pom_draftsmen WHERE code_domain=@from_domain AND code_path=@from_path";
				AddParameter(cmd, "from_domain", DbType.String, fromCode.Domain);
				AddParameter(cmd, "from_path", DbType.String, fromCode.Path);
				AddParameter(cmd, "to_domain", DbType.String, toCode.Domain);
				AddParameter(cmd, "to_path", DbType.String, toCode.Path);

				cmd.ExecuteNonQuery();
			}

			int affected;
			using(SqliteCommand cmd = conn.CreateCommand())
			{
				cmd.CommandText = "DELETE FROM pom_draftsmen WHERE code_domain=@domain AND code_path=@path";

				AddParameter(cmd, "domain", DbType.String, fromCode.Domain);
				AddParameter(cmd, "path", DbType.String, fromCode.Path);

				affected = cmd.ExecuteNonQuery();
			}

			transaction.Commit();
			return affected;
		}

		public void GetPlayerDrafts(string playerUID, ICollection<AssetLocation> outList)
		{
			using SqliteCommand cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT code_domain, code_path FROM pom_draftsmen WHERE player=@player";

			AddParameter(cmd, "player", DbType.String, playerUID);

			using var reader = cmd.ExecuteReader();
			var domainId = reader.GetOrdinal("code_domain");
			var pathId = reader.GetOrdinal("code_path");
			while(reader.Read())
			{
				outList.Add(new AssetLocation(reader.GetString(domainId), reader.GetString(pathId)));
			}
		}

		public void AddPlayerDraft(string playerUID, AssetLocation code)
		{
			try
			{
				using SqliteCommand cmd = conn.CreateCommand();
				cmd.CommandText = "INSERT OR IGNORE INTO pom_draftsmen (player,code_domain,code_path) VALUES (@player,@domain,@path)";

				AddParameter(cmd, "player", DbType.String, playerUID);
				AddParameter(cmd, "domain", DbType.String, code.Domain);
				AddParameter(cmd, "path", DbType.String, code.Path);

				cmd.ExecuteNonQuery();
			}
			catch(Exception e)
			{
				logger.Warning("[pomdrafts] Draft {0} was not saved for player {1} due to exception:\n{2}", code, playerUID, e);
			}
		}

		public void RemovePlayerDraft(string playerUID, AssetLocation code)
		{
			try
			{
				using SqliteCommand cmd = conn.CreateCommand();
				cmd.CommandText = "DELETE FROM pom_draftsmen WHERE player=@player AND code_domain=@domain AND code_path=@path";

				AddParameter(cmd, "player", DbType.String, playerUID);
				AddParameter(cmd, "domain", DbType.String, code.Domain);
				AddParameter(cmd, "path", DbType.String, code.Path);

				cmd.ExecuteNonQuery();
			}
			catch(Exception e)
			{
				logger.Warning("[pomdrafts] Draft {0} was not saved for player {1} due to exception:\n{2}", code, playerUID, e);
			}
		}

		public void AddPlayerDrafts<T>(string playerUID, T codes) where T : IEnumerable<AssetLocation>
		{
			try
			{
				using var transaction = conn.BeginTransaction();
				using SqliteCommand cmd = conn.CreateCommand();
				cmd.CommandText = "INSERT OR IGNORE INTO pom_draftsmen (player,code_domain,code_path) VALUES (@player,@domain,@path)";

				AddParameter(cmd, "player", DbType.String, playerUID);

				var domainParam = PrepareParameter(cmd, "domain", DbType.String);
				var pathParam = PrepareParameter(cmd, "path", DbType.String);
				foreach(var code in codes)
				{
					domainParam.Value = code.Domain;
					pathParam.Value = code.Path;
					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch(Exception e)
			{
				logger.Warning("[pomdrafts] Drafts {0} were not saved for player {1} due to exception:\n{2}", string.Join(',', codes), playerUID, e);
			}
		}

		public void RemovePlayerDrafts<T>(string playerUID, T codes) where T : IEnumerable<AssetLocation>
		{
			try
			{
				using var transaction = conn.BeginTransaction();
				using SqliteCommand cmd = conn.CreateCommand();
				cmd.CommandText = "DELETE FROM pom_draftsmen WHERE player=@player AND code_domain=@domain AND code_path=@path";

				AddParameter(cmd, "player", DbType.String, playerUID);

				var domainParam = PrepareParameter(cmd, "domain", DbType.String);
				var pathParam = PrepareParameter(cmd, "path", DbType.String);
				foreach(var code in codes)
				{
					domainParam.Value = code.Domain;
					pathParam.Value = code.Path;
					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch(Exception e)
			{
				logger.Warning("[pomdrafts] Drafts {0} were not saved for player {1} due to exception:\n{2}", string.Join(',', codes), playerUID, e);
			}
		}

		public void ClearPlayerDrafts(string playerUID)
		{
			try
			{
				using SqliteCommand cmd = conn.CreateCommand();
				cmd.CommandText = "DELETE FROM pom_draftsmen WHERE player=@player";

				AddParameter(cmd, "player", DbType.String, playerUID);

				cmd.ExecuteNonQuery();
			}
			catch(Exception e)
			{
				logger.Warning("[pomdrafts] Drafts were not cleared for player {0} due to exception:\n{1}", playerUID, e);
			}
		}

		private static object GetField(object obj, string name)
		{
			return obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase)!.GetValue(obj)!;
		}

		private static void AddParameter(DbCommand cmd, string parameterName, DbType dbType, object value)
		{
			var param = cmd.CreateParameter();
			param.ParameterName = parameterName;
			param.DbType = dbType;
			param.Value = value;
			cmd.Parameters.Add(param);
		}

		private static DbParameter PrepareParameter(DbCommand cmd, string parameterName, DbType dbType)
		{
			var param = cmd.CreateParameter();
			param.ParameterName = parameterName;
			param.DbType = dbType;
			cmd.Parameters.Add(param);
			return param;
		}
	}
}