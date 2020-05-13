using System;
using System.Collections.Generic;
using System.Linq;

namespace DFS_Basketball
{
    class Lineup
    {
        public Tournament tournament { get; }
        public Player[] lineup { get; }
        public Game[] game_lookup { get; }
        public int[] salary_lookup { get; }
        public int[] proj_fp_lookup { get; }
        public double cost { get; set; }
        public double pred_score { get; set; }
        public double true_score { get; set; }
        public double rsquared_sum { get; set; }
        public double rmse_sum { get; set; }

        public Lineup(Tournament tournament)
        {
            this.tournament = tournament;

            // { PG, SG, G, SF, PF, F, C, Util }
            this.lineup = new Player[8];
            this.game_lookup = new Game[8];
            this.salary_lookup = new int[8];
            this.proj_fp_lookup = new int[8];

            this.cost = 0;
            this.pred_score = 0;
            this.true_score = 0;

            this.rsquared_sum = 0;
            this.rmse_sum = 0;

        }

        public Lineup(Lineup parent)
        {
            this.tournament = parent.tournament;

            // { PG, SG, G, SF, PF, F, C, Util }
            this.lineup = new Player[8];
            this.game_lookup = new Game[8];
            this.salary_lookup = new int[8];
            this.proj_fp_lookup = new int[8];

            this.cost = 0;
            this.pred_score = 0;
            this.true_score = 0;

            this.rsquared_sum = 0;
            this.rmse_sum = 0;

            // Fill in players from parent
            for (int i = 0; i < lineup.Length; i++)
            {
                Player p = parent.lineup[i];
                if (p != null)
                {
                    Game g = parent.game_lookup[i];
                    int proj_fp = parent.proj_fp_lookup[i];
                    this.AddPlayer(p, g, proj_fp);
                }
            }
        }

        public int AddPlayer(Player player, Game game, int proj_fp, bool past = true)
        {
            int index = -1; // We'll return the index in the array that we put the player

            switch (player.position)
            {
                case "PG":
                    if (this.lineup[0] == null)
                    {
                        index = 0;
                    }
                    else if (this.lineup[2] == null)
                    {
                        index = 2;
                    }
                    else if (this.lineup[7] == null)
                    {
                        index = 7;
                    }
                    break;
                case "SG":
                    if (this.lineup[1] == null)
                    {
                        index = 1;
                    }
                    else if (this.lineup[2] == null)
                    {
                        index = 2;
                    }
                    else if (this.lineup[7] == null)
                    {
                        index = 7;
                    }
                    break;
                case "SF":
                    if (this.lineup[3] == null)
                    {
                        index = 3;
                    }
                    else if (this.lineup[5] == null)
                    {
                        index = 5;
                    }
                    else if (this.lineup[7] == null)
                    {
                        index = 7;
                    }
                    break;
                case "PF":
                    if (this.lineup[4] == null)
                    {
                        index = 4;
                    }
                    else if (this.lineup[5] == null)
                    {
                        index = 5;
                    }
                    else if (this.lineup[7] == null)
                    {
                        index = 7;
                    }
                    break;
                case "C":
                    if (this.lineup[6] == null)
                    {
                        index = 6;
                    }
                    else if (this.lineup[7] == null)
                    {
                        index = 7;
                    }
                    break;
            }

            if (index != -1)
            {
                lineup[index] = player;
                game_lookup[index] = game;
                salary_lookup[index] = player.salary_lookup[game];
                proj_fp_lookup[index] = proj_fp;

                this.cost += this.salary_lookup[index];
                this.pred_score += proj_fp;

                // If this is for predicting something in the past, check what the true score was
                if (past)
                {
                    true_score += player.game_stats[game][19];
                }

                this.rsquared_sum += player.model_metrics.RSquared;
                this.rmse_sum += player.model_metrics.RootMeanSquaredError;
            }

            return index;
        }

        public bool IsValid(int salary_cap = 200)
        {
            return
                // Make sure all positions are filled
                isFull() &&

                // All entrants must draft athletes for their lineups from at least three(3) different teams
                this.lineup.Select(p => p.team_id).Distinct().Count() >= 3 &&

                // Make sure total salary is no more than the cap
                cost <= salary_cap;
        }

        public bool isFull()
        {
            // Make sure all slots are full
            for (int i = 0; i < this.lineup.Length; i++)
            {
                if (this.lineup[i] == null)
                {
                    return false;
                }
            }
            return true;
        }

        public double GetRsquared()
        {
            return this.rsquared_sum / this.lineup.Count();
        }

        public double GetRMSE()
        {
            return this.rmse_sum / this.lineup.Count();
        }

        public override string ToString()
        {
            string players = lineup.Count() + " players\n" +
                pred_score + " predicted points\n" +
                cost + " total cost\n[";
            for (int i = 0; i < this.lineup.Length; i++)
            {
                Player p = this.lineup[i];
                players += "\t" + p.name + "\t$" + this.salary_lookup[i] + "\t" + this.proj_fp_lookup[i] + "\t" + p.position + "\n";
            }
            players += "]";
            return players;
        }
    }
}
