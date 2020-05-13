using System;

namespace DFS_Basketball
{
    class Game
    {
        public Team home { get; }
        public string home_team_id { get; }
        public Team away { get; }
        public string away_team_id { get; }
        public DateTime datetime { get; }
        public string gameid { get; }
        public string global_gameid { get; }
        public string box_score_url { get; }
        public Tournament tournament { get; }
        public int season { get; }
        public Game(string gameid, string global_gameid, string home_team_id, string away_team_id, string box_score_url, string dt, Tournament tournament)
        {
            this.gameid = gameid;
            this.global_gameid = global_gameid;
            this.home_team_id = home_team_id;
            this.away_team_id = away_team_id;
            this.box_score_url = box_score_url;
            this.datetime = DateTime.Parse(dt);

            // Set season. Yahoo! looks at seasons from start year, not end year
            this.season = this.datetime.Year;
            // If January-June, season is actually a year back
            if (this.datetime.Month < 7)
            {
                this.season--;
            }

            // Set tournament and connect them
            this.tournament = tournament;
            this.tournament.AddGame(this);
        }

    }
}