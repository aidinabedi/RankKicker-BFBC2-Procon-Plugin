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
	public class CRankKicker : PRoConPluginAPI, IPRoConPluginInterface
	{
		private static readonly string className = typeof(CRankKicker).Name;

		private readonly HashSet<string> m_reservedPlayers;

		private bool m_isPluginEnabled;

		private int m_rankLimit = 49;
		private int m_checkInterval = 5;
		private enumBoolYesNo m_allowReservedPlayers = enumBoolYesNo.No;

		public CRankKicker()
		{
			m_reservedPlayers = new HashSet<string>();
		}

		public string GetPluginName()
		{
			return "Rank Kicker";
		}

		public string GetPluginVersion()
		{
			return "1.1.0.0";
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
			return @"Kicks a player if they have to high rank.";
		}

		public List<CPluginVariable> GetDisplayPluginVariables()
		{
			return GetPluginVariables();
		}

		// Lists all of the plugin variables.
		public List<CPluginVariable> GetPluginVariables()
		{
			var retval = new List<CPluginVariable>();

			retval.Add(new CPluginVariable("Rank Limit", typeof(int), m_rankLimit));
			retval.Add(new CPluginVariable("Check Interval", typeof(int), m_checkInterval));
			retval.Add(new CPluginVariable("Allow Reserved Players", typeof(enumBoolYesNo), m_allowReservedPlayers));

			return retval;
		}

		public void OnPluginLoaded(string hostName, string port, string proconVersion)
		{
			RegisterEvents(className, "OnPlayerJoin", "OnListPlayers", "OnReservedSlotsPlayerAdded", "OnReservedSlotsPlayerRemoved", "OnReservedSlotsList", "OnReservedSlotsCleared");
		}

		public void OnPluginEnable()
		{
			m_isPluginEnabled = true;
			m_reservedPlayers.Clear();

			ExecuteCommand("procon.protected.send", "reservedSlots.list");
			ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");

			ExecuteCommand("procon.protected.pluginconsole.write", "^b" + GetPluginName() + " ^2Enabled!" );

			_UpdateCheckInterval();
		}

		public void OnPluginDisable()
		{
			m_isPluginEnabled = false;
			m_reservedPlayers.Clear();

			ExecuteCommand("procon.protected.tasks.remove", className);

			ExecuteCommand("procon.protected.pluginconsole.write", "^b" + GetPluginName() + " ^1Disabled =(" );
		}

		public void SetPluginVariable(string variable, string value)
		{
			if (variable == "Rank Limit")
			{
				int.TryParse(value, out m_rankLimit);
			}
			else if (variable == "Check Interval")
			{
				int.TryParse(value, out m_checkInterval);
				_UpdateCheckInterval();
			}
			else if (variable == "Allow Reserved Players" && Enum.IsDefined(typeof(enumBoolYesNo), value))
			{
				m_allowReservedPlayers = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
		}

		private void _UpdateCheckInterval() 
		{
			ExecuteCommand("procon.protected.tasks.remove", className);

			if (m_isPluginEnabled && m_checkInterval != 0)
			{
				ExecuteCommand("procon.protected.tasks.add", className, "0", m_checkInterval.ToString(), "-1", "procon.protected.send", "admin.listPlayers", "all");
			}
		}

		public override void OnPlayerJoin(string soldierName)
		{
			try
			{
				ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".OnPlayerJoin Exception: " + e.Message);
			}
		}

		public override void OnReservedSlotsPlayerAdded(string soldierName)
		{
			try
			{
				m_reservedPlayers.Add(soldierName);
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".OnReservedSlotsPlayerAdded Exception: " + e.Message);
			}
		}

		public override void OnReservedSlotsPlayerRemoved(string soldierName)
		{
			try
			{
				m_reservedPlayers.Remove(soldierName);
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".OnReservedSlotsPlayerRemoved Exception: " + e.Message);
			}
		}

		public override void OnReservedSlotsList(List<string> soldierNames)
		{
			try
			{
				m_reservedPlayers.Clear();
				
				foreach (var soldierName in soldierNames)
				{
					m_reservedPlayers.Add(soldierName);
				}
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".OnReservedSlotsList Exception: " + e.Message);
			}
		}

		public override void OnReservedSlotsCleared()
		{
			try
			{
				m_reservedPlayers.Clear();
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".OnReservedSlotsCleared Exception: " + e.Message);
			}
		}

		public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
		{
			try
			{
				foreach (var player in players)
				{
					var soldierName = player.SoldierName;
					if (m_allowReservedPlayers == enumBoolYesNo.Yes && m_reservedPlayers.Contains(soldierName)) continue;

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

					if (rank > m_rankLimit)
					{
						ExecuteCommand("procon.protected.send", "admin.kickPlayer", soldierName, "You got kicked due to your Player Rank (" + rank + ") being too high!");
						ExecuteCommand("procon.protected.send", "admin.say", "Kicked '" + soldierName + "' because their Player Rank (" + rank + ") is too high!");
					}
				}
			}
			catch (Exception e)
			{
				ExecuteCommand("procon.protected.pluginconsole.write", className + ".OnListPlayers Exception: " + e.Message);
			}
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
