using System;
using System.Collections.Generic;

namespace DFS_Basketball
{
    class Tournament
    {
        public string id { get; }
        public DateTime datetime { get; }
        double perfect_lineup_salary { get; }
        public double perfect_lineup_fp { get; }
        double best_submitted_salary { get; }
        public double best_submitted_fp { get; }
        public double average_fp { get; }
        public double stdev_fp { get; }
        public List<Game> games { get; }
        public Tournament(string id, DateTime datetime,
            string perfect_lineup_salary, string perfect_lineup_fp,
            string best_submitted_salary, string best_submitted_fp,
            string average_fp, string stdev_fp)
        {
            this.id = id;
            this.datetime = datetime;
            this.perfect_lineup_salary = Double.Parse(perfect_lineup_salary);
            this.perfect_lineup_fp = Double.Parse(perfect_lineup_fp);
            this.best_submitted_salary = Double.Parse(best_submitted_salary);
            this.best_submitted_fp = Double.Parse(best_submitted_fp);
            this.average_fp = Double.Parse(average_fp);
            this.stdev_fp = Double.Parse(stdev_fp);

            this.games = new List<Game>();
        }

        public Tournament(DateTime datetime)
        {
            this.id = "CURRENT_CONTEST";
            this.datetime = datetime;
        }

        public void AddGame(Game g)
        {
            games.Add(g);
        }
    }
}