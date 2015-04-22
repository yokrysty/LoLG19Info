using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace LoLG19Info
{
    public class LoLNexusParser
    {
        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2272.118 Safari/537.36";
        private const string ACCEPT = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
        private const string CONTENT_TYPE = "application/json; charset=utf-8";

        private int RetryAttempts { get; set; }
        private int RetryTimeMillis { get; set; }
        private bool state = false;

        private Array[] teams;


        public LoLNexusParser()
        {
            this.RetryAttempts = 3;
            this.RetryTimeMillis = 10000;
        }

        public void Parse(string summoner, string region = "EUNE")
        {
            this.state = false;
            int num = RetryAttempts;
            while (!(this.state || num == 0))
            {
                this.state = this.getLoLNexusInfo(summoner, region);
                if (!this.state)
                {
                    Thread.Sleep(this.RetryTimeMillis);
                }
                num--;
            }
        }

        private bool getLoLNexusInfo(string summoner, string server)
        {
            try
            {
                string url = "http://www.lolnexus.com/ajax/get-game-info/" + server + ".json?name=" + summoner.Replace(" ", "%20");

                HttpWebRequest webRequest = WebRequest.Create(url) as HttpWebRequest;
                webRequest.UserAgent = USER_AGENT;
                webRequest.Accept = ACCEPT;
                webRequest.ContentType = CONTENT_TYPE;
                webRequest.Method = "GET";

                HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();

                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding(response.CharacterSet)))
                {
                    string result = reader.ReadToEnd().Replace(@"\r\n", "").Replace(@"\", "").Replace("\"", "");

                    string[] strArrTeams = result.Split(new string[] { "team-1" }, StringSplitOptions.None)[1].Split(new string[] { "team-2" }, StringSplitOptions.None);

                    this.teams = new Array[strArrTeams.Length];
                    LoLNexusInfo[] summoners;

                    for (int i = 0; i <= strArrTeams.Length - 1; i++)
                    {
                        string[] strArrPlayers = strArrTeams[i].Split(new string[] { "class=name>" }, StringSplitOptions.None);

                        //skip first 2 items in players array (table header)
                        summoners = new LoLNexusInfo[strArrPlayers.Length - 2];

                        for (int j = 2; j <= strArrPlayers.Length - 1; j++)
                        {
                            int index = j - 2;

                            summoners[index] = new LoLNexusInfo();

                            summoners[index].Summoner = GetStringBetween(strArrPlayers[j], "<span>", "</span>");

                            summoners[index].Champion = GetStringBetween(strArrPlayers[j], "<span>", "(<b", 2).Replace("&#x27;", "'");

                            string[] strArr = GetStringBetween(strArrPlayers[j], "Level</h2>", "</span>").Split(new string[] { ">" }, StringSplitOptions.None);

                            summoners[index].Level = strArr[0];

                            summoners[index].Wins = strArr[1].Replace(",", " ").Replace(".", " ");

                            for (int k = 1; k <= 2; k++)
                            {
                                string spellName = GetStringBetween(strArrPlayers[j], "<img class=summoner-spell", "/>", k).ToLower().Trim();

                                foreach (Spells spell in Enum.GetValues(typeof(Spells)))
                                {
                                    if (spellName.Contains(spell.ToString()))
                                    {
                                        summoners[index].Spells[k - 1] = spell.ToString();
                                        break;
                                    }
                                }
                            }

                            summoners[index].Division = GetStringBetween(strArrPlayers[j], "class=champion-ranks", "(<b").Split(new string[] { ">" }, StringSplitOptions.None)[1];

                            if (summoners[index].Summoner == summoner)
                            {
                                summoners[index].me = true;
                            }
                        }
                        this.teams[i] = summoners;
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetStringBetween(string input, string startString, string endString, int nth = 1)
        {
            int startIndex = IndexOfNth(input, startString, nth) + startString.Length;
            int endIndex = input.IndexOf(endString, startIndex);
            return input.Substring(startIndex, endIndex - startIndex).Trim();
        }

        private static int IndexOfNth(string input, string match, int nth)
        {
            int i = 1;
            int index = 0;

            while (i <= nth && (index = input.IndexOf(match, index + 1)) != -1)
            {
                if (i == nth)
                    return index;

                i++;
            }
            return -1;
        }

        private enum Spells
        {
            barrier,
            clairvoyance,
            clarity,
            cleanse,
            exhaust,
            flash,
            garrison,
            ghost,
            heal,
            ignite,
            smite,
            teleport
        }

        public bool Success
        {
            get { return this.state; }
        
        }

        public Array getData()
        {
            return this.teams;
        }
    }

    public class LoLNexusInfo
    {
        public bool me = false;
        public string Summoner;
        public string Level;
        public string Champion;
        public string Wins;
        public string Division;
        public string[] Spells = new string[2];
    }
}