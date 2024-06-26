using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PowerOfMind.Drafts
{
	public partial class DraftsModSystem
	{
		private void RegisterCommands(ICoreServerAPI api)
		{
			var parsers = api.ChatCommands.Parsers;
			api.ChatCommands.Create("pomdrafts")
				.WithDescription("Player drafts manager")
				.RequiresPrivilege(Privilege.controlserver)
				.BeginSubCommand("learn")
				.WithDescription("Add draft to player")
					.RequiresPlayer()
					.WithArgs(parsers.Word("domainAndPath"), parsers.OptionalWord("playerName"))
					.HandleWith(CmdAddPlayerDraft)
				.EndSubCommand()
				.BeginSubCommand("remove")
					.RequiresPlayer()
					.WithDescription("Remove a player's draft")
					.WithArgs(parsers.Word("domainAndPath"), parsers.OptionalWord("playerName"))
					.HandleWith(CmdRemovePlayerDraft)
				.EndSubCommand()
				.BeginSubCommand("clear")
					.RequiresPlayer()
					.WithDescription("Clear player drafts")
					.WithArgs(parsers.OptionalWord("playerName"))
					.HandleWith(CmdClearPlayerDrafts)
				.EndSubCommand()
				.BeginSubCommand("learnall")
					.RequiresPlayer()
					.WithDescription("Add all available drafts for a player")
					.WithArgs(parsers.OptionalWord("playerName"))
					.HandleWith(CmdAddAllPlayerDrafts)
				.EndSubCommand()
				.BeginSubCommand("list")
					.RequiresPlayer()
					.WithDescription("List player draft codes")
					.WithArgs(parsers.OptionalWord("playerName"))
					.HandleWith(CmdListPlayerDrafts)
				.EndSubCommand()
				.BeginSubCommand("codes")
					.WithDescription("List all draft codes")
					.HandleWith(CmdListAllDrafts)
				.EndSubCommand()
				.BeginSubCommand("remap")
					.WithDescription("Remap draft code (replace existing codes for players)")
					.WithArgs(parsers.Word("from"), parsers.Word("to"))
					.HandleWith(CmdRemapDraft)
				.EndSubCommand();
		}

		private TextCommandResult CmdAddPlayerDraft(TextCommandCallingArgs args)
		{
			bool hasPlayerArg = args.ArgCount == 3;
			using var playerDrafts = GetPlayerDraftsForCmd(args, hasPlayerArg ? 1 : null, out var failResult);
			if(playerDrafts == null) return failResult;

			var code = new AssetLocation((string)(hasPlayerArg ? args[1] : args[0]));
			if(!assetsSystem.DraftDescriptors.ContainsKey(code))
			{
				return TextCommandResult.Success($"Draft {code} does not exist");
			}

			if(playerDrafts.Contains(code))
			{
				return TextCommandResult.Success($"Player already has draft {code}, nothing has been changed");
			}

			using var change = playerDrafts.BeginChange();
			change.Add(code);

			return TextCommandResult.Success($"Successfully added draft {code} to player");
		}

		private TextCommandResult CmdRemovePlayerDraft(TextCommandCallingArgs args)
		{
			bool hasPlayerArg = args.ArgCount == 3;
			using var playerDrafts = GetPlayerDraftsForCmd(args, hasPlayerArg ? 1 : null, out var failResult);
			if(playerDrafts == null) return failResult;

			var code = new AssetLocation((string)(hasPlayerArg ? args[1] : args[0]));
			if(!playerDrafts.Contains(code))
			{
				return TextCommandResult.Success($"Player has no draft {code}, nothing has been changed");
			}

			using var change = playerDrafts.BeginChange();
			change.Remove(code);

			return TextCommandResult.Success($"Successfully removed draft {code} from the player");
		}

		private TextCommandResult CmdClearPlayerDrafts(TextCommandCallingArgs args)
		{
			using var playerDrafts = GetPlayerDraftsForCmd(args, args.ArgCount == 2 ? 0 : null, out var failResult);
			if(playerDrafts == null) return failResult;

			using var change = playerDrafts.BeginChange();
			change.Clear();

			return TextCommandResult.Success($"Successfully cleared player drafts");
		}

		private TextCommandResult CmdAddAllPlayerDrafts(TextCommandCallingArgs args)
		{
			using var playerDrafts = GetPlayerDraftsForCmd(args, args.ArgCount == 2 ? 0 : null, out var failResult);
			if(playerDrafts == null) return failResult;

			var added = new HashSet<AssetLocation>(assetsSystem.DraftDescriptors.Keys);
			added.ExceptWith(playerDrafts);

			if(added.Count == 0)
			{
				return TextCommandResult.Success($"Player already has all drafts,nothing has been changed");
			}

			using var change = playerDrafts.BeginChange();
			change.AddRange(added);

			return TextCommandResult.Success($"Successfully added {added.Count} drafts to player");
		}

		private TextCommandResult CmdListPlayerDrafts(TextCommandCallingArgs args)
		{
			using var playerDrafts = GetPlayerDraftsForCmd(args, args.ArgCount == 2 ? 0 : null, out var failResult);
			if(playerDrafts == null) return failResult;

			var logger = sapi.Logger;
			logger.Notification("Current list of player drafts (issued by /pomdrafts list command)");
			var sb = new StringBuilder();
			bool hasMore = false;
			foreach(var code in playerDrafts)
			{
				logger.Notification("\t" + code);
				if(sb.Length < 8096)
				{
					if(sb.Length != 0) sb.Append(", ");
					sb.Append(code);
				}
				else
				{
					hasMore = true;
				}
			}
			if(hasMore)
			{
				sb.Append("...");
				sb.AppendLine();
				sb.Append("Full list printed to console and main log file");
			}
			return TextCommandResult.Success(sb.ToString());
		}

		private TextCommandResult CmdListAllDrafts(TextCommandCallingArgs args)
		{
			var logger = sapi.Logger;
			logger.Notification("Current list of registered drafts (issued by /pomdrafts codes command)");
			foreach(var pair in assetsSystem.DraftDescriptors)
			{
				logger.Notification("\t" + pair.Key);
			}
			return TextCommandResult.Success("Full list printed to console and main log file");
		}

		private TextCommandResult CmdRemapDraft(TextCommandCallingArgs args)
		{
			var from = new AssetLocation((string)args[0]);
			var to = new AssetLocation((string)args[1]);

			int count = 0;
			if(!from.Equals(to))
			{
				count = GetDatabase().Remap(from, to);
				foreach(var pair in draftsByPlayer)
				{
					if(pair.Value.Contains(from))
					{
						using(var change = pair.Value.BeginChange())
						{
							change.Remove(from);
							change.Add(to);
						}
					}
				}
			}

			return TextCommandResult.Success($"Successfully remapped {count} records");
		}

		private IServerPlayer? TryGetPlayer(TextCommandCallingArgs args, string? playerName)
		{
			if(!string.IsNullOrEmpty(playerName))
			{
				foreach(var player in sapi.Server.Players)
				{
					if(player.PlayerName.Equals(playerName, StringComparison.InvariantCultureIgnoreCase))
					{
						return player;
					}
				}
				return null;
			}
			return args.Caller.Player as IServerPlayer;
		}

		private IMutablePlayerDrafts? GetPlayerDraftsForCmd(TextCommandCallingArgs args, int? playerNameArg, out TextCommandResult result)
		{
			string? playerName = null;
			if(playerNameArg.HasValue)
			{
				playerName = (string)args[playerNameArg.Value];
			}

			var player = TryGetPlayer(args, playerName);
			if(player == null)
			{
				result = TextCommandResult.Success($"Player does not exist or is not online");
				return null;
			}

			var playerDrafts = GetPlayerDrafts(player.PlayerUID);
			if(playerDrafts == null)
			{
				result = TextCommandResult.Success($"Player does not exist or is not online");
				return null;
			}

			result = null!;
			return playerDrafts;
		}
	}
}