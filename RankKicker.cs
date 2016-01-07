using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;
using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
	public class RankKicker : PRoConPluginAPI, IPRoConPluginInterface
	{
		private static readonly string className = typeof(RankKicker).Name;

		private bool isPluginEnabled;
		private readonly HashSet<string> reservedPlayers = null;

		private int rankLimit = 49;
		private int checkInterval = 5;
		private bool ignoreReservedPlayers = false;
		private bool ignoreCase = false;

		public string GetPluginName()
		{
			return "Rank Kicker";
		}

		public string GetPluginVersion()
		{
			return "1.3.0.0";
		}

		public string GetPluginAuthor()
		{
			return "aidinabedi";
		}

		public string GetPluginWebsite()
		{
			return "";
		}

		public string GetPluginDescription()
		{
			return @"Kicks a player if they have too high rank.";
		}

		public List<CPluginVariable> GetDisplayPluginVariables()
		{
			return GetPluginVariables();
		}

		public List<CPluginVariable> GetPluginVariables()
		{
			return new List<CPluginVariable>
			{
				new CPluginVariable("Rank Limit", typeof(int), rankLimit),
				new CPluginVariable("Check Interval", typeof(int), checkInterval),
				new CPluginVariable("Ignore Reserved Players", typeof(bool), ignoreReservedPlayers),
				new CPluginVariable("Case Insensitive Comparison", typeof(bool), ignoreCase),
			};
		}

		public void OnPluginLoaded(string hostName, string port, string proconVersion)
		{
			RegisterEvents(className, "OnPlayerJoin", "OnListPlayers", "OnReservedSlotsPlayerAdded", "OnReservedSlotsPlayerRemoved", "OnReservedSlotsList", "OnReservedSlotsCleared");
		}

		public void OnPluginEnable()
		{
			isPluginEnabled = true;
			reservedPlayers.Clear();

			ExecuteCommand("procon.protected.send", "reservedSlots.list");

			ExecuteCommand("procon.protected.pluginconsole.write", "^b" + GetPluginName() + " ^2Enabled!" );

			_UpdateCheckInterval();
		}

		public void OnPluginDisable()
		{
			isPluginEnabled = false;
			reservedPlayers.Clear();

			ExecuteCommand("procon.protected.tasks.remove", className);

			ExecuteCommand("procon.protected.pluginconsole.write", "^b" + GetPluginName() + " ^1Disabled =(" );
		}

		public void SetPluginVariable(string variable, string value)
		{
			switch (variable)
			{
				case "Rank Limit":
					int.TryParse(value, out rankLimit);
					break;

				case "Check Interval":
					int.TryParse(value, out checkInterval);
					_UpdateCheckInterval();
					break;

				case "Ignore Reserved Players":
					bool.TryParse(value, out ignoreReservedPlayers);
					break;

				case "Case Insensitive Comparison":
					if (bool.TryParse(value, out ignoreCase)) _UpdateIgnoreCase();
					break;
			}
		}

		private void _UpdateCheckInterval() 
		{
			ExecuteCommand("procon.protected.tasks.remove", className);

			if (isPluginEnabled && checkInterval != 0)
			{
				ExecuteCommand("procon.protected.tasks.add", className, "0", checkInterval.ToString(), "-1", "procon.protected.send", "admin.listPlayers", "all");
			}
		}

		private void _UpdateIgnoreCase() 
		{
			if (reservedPlayers != null && reservedPlayers.Comparer != _GetStringComparer())
			{
				reservedPlayers = new HashSet<string>(reservedPlayers, _GetStringComparer());
			}
		}

		public override void OnPlayerJoin(string soldierName)
		{
			_CheckAllPlayers();
		}

		public override void OnReservedSlotsPlayerAdded(string soldierName)
		{
			if (reservedPlayers == null)
			{
				reservedPlayers = new HashSet<string>(soldierNames, _GetStringComparer());
			}

			reservedPlayers.Add(soldierName);
		}

		public override void OnReservedSlotsPlayerRemoved(string soldierName)
		{
			if (reservedPlayers != null)
			{
				reservedPlayers.Remove(soldierName);

				_CheckAllPlayers();
			}
		}

		public override void OnReservedSlotsList(List<string> soldierNames)
		{
			reservedPlayers = (soldierNames.Count > 0) ? new HashSet<string>(soldierNames, _GetStringComparer()) : null;

			CheckAllPlayers();
		}

		public override void OnReservedSlotsCleared()
		{
			reservedPlayers = null;
		}

		public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
		{
			foreach (var player in players)
			{
				var soldierName = player.SoldierName;
				if (ignoreReservedPlayers && reservedPlayers != null && reservedPlayers.Contains(soldierName))
				{
					continue;
				}

				int rank = player.Rank;
				if (rank == 0)
				{
					rank = _GetGameTrackerBC2Stats(soldierName);
					if (rank == 0)
					{
						rank = _GetBFBCStatRank(soldierName);
						if (rank == 0) continue;
					}
				}

				if (rank > rankLimit)
				{
					ExecuteCommand("procon.protected.send", "admin.kickPlayer", soldierName, "You got kicked due to your Player Rank (" + rank + ") being too high!");
					ExecuteCommand("procon.protected.send", "admin.say", "Kicked '" + soldierName + "' because their Player Rank (" + rank + ") is too high!");
				}
			}
		}

		private void _CheckAllPlayers()
		{
			ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
		}

		private StringComparer _GetStringComparer()
		{
			return ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		}

		private void _KickPlayer(string soldierName, int rank)
		{
			ExecuteCommand("procon.protected.send", "admin.kickPlayer", soldierName, "You got kicked due to your Player Rank (" + rank + ") being too high!");
			ExecuteCommand("procon.protected.send", "admin.say", "Kicked '" + soldierName + "' because their Player Rank (" + rank + ") is too high!");
		}

		private int _GetGameTrackerBC2Stats(string soldierName)
		{
			const string rankPrefix = "<img src=\"/images/bc2/r0";

			try
			{
				string result;

				/* Getting GameTracker BC2 stats */
				using (var wc = new WebClient())
				{
					string url = "http://www.gametracker.com/games/bc2/stats/" + _UrlEncode(soldierName) + "/";

					result = wc.DownloadString(url);
					if (String.IsNullOrEmpty(result)) return 0;
				}

				int index = result.IndexOf(rankPrefix, 0, result.Length - 2, StringComparison.OrdinalIgnoreCase);
				if (index < 0) return 0;

				string rankString = result.Substring(index + rankPrefix.Length, 2);

				int rank;
				return int.TryParse(rankString, out rank) ? rank : 0;
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".GetGameTrackerBC2Stats Exception: " + e.Message);
				return 0;
			}
		}

		private int _GetBFBCStatRank(string soldierName)
		{
			try
			{
				string result;

				/* Getting BFBC2 stats */
				using (var wc = new WebClient())
				{
					string url = "http://api.bfbcs.com/api/pc?players=" + _UrlEncode(soldierName) + "&fields=smallinfo";

					result = wc.DownloadString(url);
					if (String.IsNullOrEmpty(result)) return 0;
				}

				var data = (Hashtable)JSON.JsonDecode(result);

				double found;
				if (!(data.Contains("found") && Double.TryParse(data["found"].ToString(), out found) == true && found == 1))
					/* could not find stats */
					return 0;

				/* interpret the results from BFBC stats */
				Hashtable playerData = (Hashtable)((ArrayList)data["players"])[0];

				double rank;
				double.TryParse(playerData["rank"].ToString(), out rank);

				return (int)rank;
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".GetBFBCStatRank Exception: " + e.Message);
				return 0;
			}
		}

		private static string _UrlEncode(string text)
		{
			var sb = new StringBuilder(text.Length * 6);

			foreach (char c in text)
			{
				if (('0' <= c && c <= '9') || ('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z') || (c == '*' || c == '-' || c == '.' || c == '_'))
				{
					sb.Append(c);
				}
				else if (c <= '\u007F')
				{
					sb.Append('%');
					sb.Append(((int)c).ToString("X2"));
				}
				else
				{
					foreach (byte b in Encoding.UTF8.GetBytes(c.ToString()))
					{
						sb.Append('%');
						sb.Append(b.ToString("X2"));
					}
				}
			}

			return sb.ToString();
		}
	}
}
