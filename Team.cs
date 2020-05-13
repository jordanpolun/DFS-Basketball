using System;
using System.Collections.Generic;
using System.Linq;

namespace DFS_Basketball
{
    class Team
    {
        public string team_id { get; }
        public string name { get; }
        public string location { get; }
        public string team_abbr { get; }
        public Dictionary<string, double> ratings_2017 { get; }
        public Dictionary<string, double> ratings_2018 { get; }
        public Dictionary<string, double> ratings_2019 { get; }

        public Team(string team_id, string team_name, string location)
        {
            // Basic team data
            this.team_id = team_id;
            this.name = team_name;
            this.location = location;

            // Initialize ratings list for each season
            this.ratings_2017 = new Dictionary<string, double>();
            this.ratings_2018 = new Dictionary<string, double>();
            this.ratings_2019 = new Dictionary<string, double>();

            string[] team_abbreviations = new string[]
            {
                "ATL", "BOS", "NO", "CHI", "CLE", "DAL",
                "DEN", "DET", "GS", "HOU", "IND", "LAC",
                "LAL", "MIA", "MIL", "MIN", "BKN", "NY",
                "ORL", "PHI", "PHO", "POR", "SAC", "SA",
                "OKC", "UTA", "WAS", "TOR", "MEM", "CHA"
            };

            this.team_abbr = team_abbreviations[Int32.Parse(this.team_id.Substring(this.team_id.LastIndexOf('.') + 1)) - 1];
        }

        public string GetBBRefCode(string code)
        {
            if (code.Equals("BKN"))
            {
                return "BRK";
            }
            if (code.Equals("CHA"))
            {
                return "CHO";
            }
            if (code.Equals("PHX"))
            {
                return "PHO";
            }
            if (code.Equals("GS"))
            {
                return "GSW";
            }
            if (code.Equals("NO"))
            {
                return "NOP";
            }
            if (code.Equals("NY"))
            {
                return "NYK";
            }
            if (code.Equals("SA"))
            {
                return "SAS";
            }
            return code;
        }

        public void AddRatings(Dictionary<string, double> ratings, int years_ago = 0)
        {
            Dictionary<string, double>[] dicts = new Dictionary<string, double>[]
            {
                ratings_2019, ratings_2018, ratings_2017
            };

            ratings.ToList().ForEach(x => dicts[years_ago].Add(x.Key, x.Value));
        }
    }
}