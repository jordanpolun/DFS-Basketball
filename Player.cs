using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DFS_Basketball
{
    class Player
    {
        public string player_id { get; }
        public string team_id { get; }
        public Dictionary<Game, List<double>> game_stats { get; }
        internal Dictionary<Game, string> opp_lookup { get; }
        public Dictionary<Game, int> salary_lookup { get; }
        public string name { get; }
        public string position { get; set; }
        public Team team { get; set; }
        public PredictionEngine<GameStats, ProjFpGameStats> model { get; set; }
        public RegressionMetrics model_metrics { get; set; }
        public Player(string player_id, string name, string team_id, string position)
        {
            this.player_id = player_id;
            this.name = name;
            this.team_id = team_id;
            this.position = position;
            this.game_stats = new Dictionary<Game, List<double>>();
            this.opp_lookup = new Dictionary<Game, string>();
            this.salary_lookup = new Dictionary<Game, int>();
        }

        public void AddGameStats(Game game, string stat_line)
        {
            /** Keys 
             * MINUTES_PLAYED - 0
             * FIELD_GOALS_MADE - 1
             * FIELD_GOAL_ATTEMPTS - 2
             * FIELD_GOAL_PERCENTAGE - 3
             * THREE_POINTS_MADE - 4
             * THREE_POINT_ATTEMPTS - 5
             * THREE_POINT_PERCENTAGE - 6
             * FREE_THROWS_MADE - 7
             * FREE_THROW_ATTEMPTS - 8
             * FREE_THROW_PERCENTAGE - 9
             * OFFENSIVE_REBOUNDS - 10
             * DEFENSIVE_REBOUNDS - 11
             * TOTAL_REBOUNDS - 12
             * ASSISTS - 13
             * TURNOVERS - 14
             * STEALS - 15
             * BLOCKS - 16
             * FOULS - 17
             * POINTS - 18
             * DATETIME - 19
             * OPPONENT - 20
             * 
             */
            string[] stat_strings = stat_line.Split(',');
            List<double> stats = new List<double>();

            // Add MINUTES_PLAYED
            stats.Add(Double.Parse(stat_strings[0].Substring(0, stat_strings[0].IndexOf(':'))) +
                Double.Parse(stat_strings[0].Substring(stat_strings[0].IndexOf(':') + 1)) / 60);

            // Add all other stats
            foreach (string stat_string in stat_strings.Skip(1).Take(stat_strings.Length - 3))
            {
                stats.Add(Double.Parse(stat_string));
            }

            // Calculate pts + (1.2 * reb) + (1.5 * ast) + (3 * stl) + (3 * blk) - tov
            double fp = stats[18] + (1.2 * stats[12]) + (1.5 * stats[13]) + (3 * stats[15]) + (3 * stats[16]) - stats[14];
            stats.Add(fp); // stats[19]

            // Get career average
            double avg_career = 0;
            foreach (Game g in this.game_stats.Keys)
                avg_career += (this.game_stats[g][19] / this.game_stats.Count());

            // Get 30 day, 14 day, career, and season averages
            Game[] season_games = this.game_stats.Keys.Where(g => g.season == game.season && g.datetime <= game.datetime).ToArray();
            Game[] prev_30_days = season_games.Where(g => g.datetime >= game.datetime.AddDays(-30)).ToArray();
            Game[] prev_14_days = prev_30_days.Where(g => g.datetime >= game.datetime.AddDays(-14)).ToArray();

            double avg_season = 0;
            double avg_30_days = 0;
            double avg_14_days = 0;

            foreach (Game g in season_games)
            {
                // Check all games this season
                avg_season += this.game_stats[g][19];

                if (prev_30_days.Contains(g))
                    avg_30_days += this.game_stats[g][19];

                // If it was also in the last 14 days, count it
                if (prev_14_days.Contains(g))
                    avg_14_days += this.game_stats[g][19];
            }

            avg_season = season_games.Length == 0 ? avg_career : avg_season / season_games.Length;
            avg_30_days = prev_30_days.Length == 0 ? avg_career : avg_30_days / prev_30_days.Length;
            avg_14_days = prev_14_days.Length == 0 ? avg_career : avg_14_days / prev_14_days.Length;

            // Add all average fp stats
            stats.Add(avg_career); //stats[20]
            stats.Add(avg_season); //stats[21]
            stats.Add(avg_30_days); //stats[22]
            stats.Add(avg_14_days); //stats[23]

            game_stats.Add(game, stats);
            opp_lookup.Add(game, stat_strings.Last());
        }

        public double GenerateModel(Team[] teams, bool update = false)
        {
            // List for rows of data
            List<GameStats> rows = new List<GameStats>();

            // Get each game's GameStats and add it to list
            foreach (Game game in this.game_stats.Keys)
            {
                int length = this.game_stats[game].Count();

                // Get averages
                double avg_career = this.game_stats[game][length - 4];
                double avg_season = this.game_stats[game][length - 3];
                double avg_30_days = this.game_stats[game][length - 2];
                double avg_14_days = this.game_stats[game][length - 1];

                // Get opponent data
                Team opp = teams.Where(t => t.team_id.Equals(this.opp_lookup[game])).First();
                Dictionary<string, double> opp_ratings = opp.ratings_2019;
                if (game.season == 2018)
                    opp_ratings = opp.ratings_2018;
                if (game.season == 2017)
                    opp_ratings = opp.ratings_2017;

                double opp_fp_allowed = opp_ratings["pts_opp"] + (1.2 * opp_ratings["reb_opp"]) + (1.5 * opp_ratings["ast_opp"]) +
                    (3 * opp_ratings["stl_opp"]) + (3 * opp_ratings["blk_opp"]) - opp_ratings["to_opp"];

                // Find depth at team's position

                // Find cluster's average against this opponent (cluster based on position and basic fantasy stats)

                // Instantiate GameStats object and add it to list of rows
                GameStats game_data = new GameStats {
                    AvgCareer = (float)avg_career,
                    AvgSeason = (float)avg_season,
                    Avg30Days = (float)avg_30_days,
                    Avg14Days = (float)avg_14_days,
                    PtsOpp = (float)opp_ratings["pts_opp"],
                    RebOpp = (float)opp_ratings["reb"],
                    AstOpp = (float)opp_ratings["ast"],
                    StlOpp = (float)opp_ratings["stl"],
                    BlkOpp = (float)opp_ratings["blk"],
                    ToOpp = (float)opp_ratings["to"],
                    OppFpAllowed = (float)opp_fp_allowed,
                    FP = (float)this.game_stats[game][19]
                };

                rows.Add(game_data);
            }

            // Generate MLContext and load IDataView
            MLContext mlContext = new MLContext(seed: 0);
            IDataView gamestats_dataview = mlContext.Data.LoadFromEnumerable<GameStats>(rows);

            if (update)
            {
                // Create schema
                IEstimator<ITransformer> pipeline =
                    mlContext.Transforms.CopyColumns(outputColumnName: "ProjFP", inputColumnName: "FP")
                    .Append(mlContext.Transforms.Concatenate("Features", "AvgCareer", "AvgSeason", "Avg30Days", "Avg14Days",
                    "PtsOpp", "RebOpp", "AstOpp", "StlOpp", "BlkOpp", "ToOpp", "OppFpAllowed"))
                    .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                    //.Append(mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(labelColumnName: "ProjFP", featureColumnName: "Features"));
                    .Append(mlContext.Regression.Trainers.Sdca(labelColumnName: "ProjFP", featureColumnName: "Features"));

                // Generate regression model and save it
                ITransformer model = pipeline.Fit(gamestats_dataview);
                mlContext.Model.Save(model, gamestats_dataview.Schema, Directory.GetCurrentDirectory() + "/Data/Player Models/" + this.player_id + "_" + this.name + ".zip");
            }

            // Generate new MLContext
            if (File.Exists(Directory.GetCurrentDirectory() + "/Data/Player Models/" + this.player_id + "_" + this.name + ".zip"))
            {
                DataViewSchema modelSchema;
                ITransformer model_loaded = mlContext.Model.Load(Directory.GetCurrentDirectory() + "/Data/Player Models/" + this.player_id + "_" + this.name + ".zip", out modelSchema);

                // Generate prediction model
                PredictionEngine<GameStats, ProjFpGameStats> predictionFunction = mlContext.Model.CreatePredictionEngine<GameStats, ProjFpGameStats>(model_loaded);
                this.model = predictionFunction;

                // Evaluate model
                IDataView predictions = model_loaded.Transform(gamestats_dataview);
                RegressionMetrics metrics = mlContext.Regression.Evaluate(predictions, "ProjFP");
                this.model_metrics = metrics;

                return metrics.RootMeanSquaredError;
            }

            Console.WriteLine(Directory.GetCurrentDirectory() + "/Data/Player Models/" + this.player_id + "_" + this.name + ".zip does not exist");
            return -1;
        }

        public int ProjectFP(Game game, Team[] teams)
        {
            int length = this.game_stats[game].Count();

            // Get avg fantasy points from different timespans
            double avg_career = this.game_stats[game][length - 4];
            double avg_season = this.game_stats[game][length - 3];
            double avg_30_days = this.game_stats[game][length - 2];
            double avg_14_days = this.game_stats[game][length - 1];

            // Get opponent data
            Team opp = teams.Where(t => t.team_id.Equals(this.opp_lookup[game])).First();
            Dictionary<string, double> opp_ratings = opp.ratings_2019;
            if (game.season == 2018)
                opp_ratings = opp.ratings_2018;
            if (game.season == 2017)
                opp_ratings = opp.ratings_2017;

            double opp_fp_allowed = opp_ratings["pts_opp"] + (1.2 * opp_ratings["reb_opp"]) + (1.5 * opp_ratings["ast_opp"]) +
                    (3 * opp_ratings["stl_opp"]) + (3 * opp_ratings["blk_opp"]) - opp_ratings["to_opp"];

            GameStats game_data = new GameStats
            {
                AvgCareer = (float)avg_career,
                AvgSeason = (float)avg_season,
                Avg30Days = (float)avg_30_days,
                Avg14Days = (float)avg_14_days,
                PtsOpp = (float)opp_ratings["pts_opp"],
                RebOpp = (float)opp_ratings["reb"],
                AstOpp = (float)opp_ratings["ast"],
                StlOpp = (float)opp_ratings["stl"],
                BlkOpp = (float)opp_ratings["blk"],
                ToOpp = (float)opp_ratings["to"],
                OppFpAllowed = (float)opp_fp_allowed,
                FP = 0
            };

            int prediction = (int)(Math.Round(this.model.Predict(game_data).ProjFp));
            if (prediction < 0)
            {
                return 0;
            }
            return prediction;
        }

        public int ProjectFP(DateTime dt, Team opp)
        {
            // Get avg fantasy points from different timespans
            double avg_career = 0;
            foreach (Game g in this.game_stats.Keys.Where(temp_g => temp_g.datetime < dt))
                avg_career += this.game_stats[g].Last();
            avg_career /= this.game_stats.Count();

            // Set season. Yahoo! looks at seasons from start year, not end year
            int season = dt.Year;
            // If January-June, season is actually a year back
            if (dt.Month < 7)
            {
                season--;
            }

            // Get 30 day, 14 day, career, and season averages
            Game[] season_games = this.game_stats.Keys.Where(g => g.season == season && g.datetime < dt).ToArray();
            Game[] prev_30_days = season_games.Where(g => g.datetime >= dt.AddDays(-30)).ToArray();
            Game[] prev_14_days = prev_30_days.Where(g => g.datetime >= dt.AddDays(-14)).ToArray();

            double avg_season = 0;
            double avg_30_days = 0;
            double avg_14_days = 0;
            foreach (Game g in season_games)
            {
                // Check all games this season
                avg_season += this.game_stats[g].Last();

                if (prev_30_days.Contains(g))
                    avg_30_days += this.game_stats[g].Last();

                // If it was also in the last 14 days, count it
                if (prev_14_days.Contains(g))
                    avg_14_days += this.game_stats[g].Last();
            }

            avg_season = season_games.Length == 0 ? avg_career : avg_season / season_games.Length;
            avg_30_days = prev_30_days.Length == 0 ? avg_career : avg_30_days / prev_30_days.Length;
            avg_14_days = prev_14_days.Length == 0 ? avg_career : avg_14_days / prev_14_days.Length;

            // Get opponent data
            Dictionary<string, double> opp_ratings = opp.ratings_2019;
            if (season == 2018)
                opp_ratings = opp.ratings_2018;
            if (season == 2017)
                opp_ratings = opp.ratings_2017;

            double opp_fp_allowed = opp_ratings["pts_opp"] + (1.2 * opp_ratings["reb_opp"]) + (1.5 * opp_ratings["ast_opp"]) +
                    (3 * opp_ratings["stl_opp"]) + (3 * opp_ratings["blk_opp"]) - opp_ratings["to_opp"];

            GameStats game_data = new GameStats
            {
                AvgCareer = (float)avg_career,
                AvgSeason = (float)avg_season,
                Avg30Days = (float)avg_30_days,
                Avg14Days = (float)avg_14_days,
                PtsOpp = (float)opp_ratings["pts_opp"],
                RebOpp = (float)opp_ratings["reb"],
                AstOpp = (float)opp_ratings["ast"],
                StlOpp = (float)opp_ratings["stl"],
                BlkOpp = (float)opp_ratings["blk"],
                ToOpp = (float)opp_ratings["to"],
                OppFpAllowed = (float)opp_fp_allowed,
                FP = 0
            };

            int prediction = (int) (Math.Round(this.model.Predict(game_data).ProjFp));
            return prediction;
        }

        public class GameStats
        {
            [ColumnName("AvgCareer")]
            public float AvgCareer { get; set; }

            [ColumnName("AvgSeason")]
            public float AvgSeason { get; set; }

            [ColumnName("Avg30Days")]
            public float Avg30Days { get; set; }

            [ColumnName("Avg14Days")]
            public float Avg14Days { get; set; }

            [ColumnName("PtsOpp")]
            public float PtsOpp { get; set; }

            [ColumnName("RebOpp")]
            public float RebOpp { get; set; }

            [ColumnName("AstOpp")]
            public float AstOpp { get; set; }

            [ColumnName("StlOpp")]
            public float StlOpp { get; set; }

            [ColumnName("BlkOpp")]
            public float BlkOpp { get; set; }

            [ColumnName("ToOpp")]
            public float ToOpp { get; set; }

            [ColumnName("OppFpAllowed")]
            public float OppFpAllowed { get; set; }

            [ColumnName("FP")]
            public float FP { get; set; }
        }

        public class ProjFpGameStats
        {
            [ColumnName("Score")]
            public float ProjFp;
        }
    }
}