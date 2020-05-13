using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DFS_Basketball
{
    class Daily_Fantasy
    {
        public static List<string> errors = new List<string>();

        public static void Main(String[] args)
        {
            bool update_info = false;

            // Read contest file
            Console.WriteLine("Reading in contest for later use...\t");
            string[] contest_rows = File.ReadAllLines(Directory.GetCurrentDirectory() + "/Data/Contest.csv");

            Console.WriteLine("Reading in and updating tournaments...\t");
            Tournament[] tournaments = GetTournaments(new DateTime(2017, 10, 17));

            Console.WriteLine("Reading games...\t");
            Game[] games = ReadGames(tournaments);

            // We read in all tournaments, not just NBA
            // Remove any tournaments with no games involved, that means they're not nba ones
            tournaments = tournaments.Where(tourn => tourn.games.Count > 0).ToArray();

            Console.WriteLine("Reading in players...");
            Player[] players = GetPlayers(update_info, games);

            Console.WriteLine("Reading in teams...");
            Team[] teams = GetTeams(update_info, players);

            // Guess salaries for past performances using regression model based off this contest
            Console.WriteLine("Assigning past salaries...\t");
            AssignSalaries(contest_rows, players);

            // Generate regression model
            Console.WriteLine("Generating and testing regression model...\t");
            double std_errors = 0;
            int count = 0;
            foreach (Player p in players)
            {
                // Do this to calculate average RMSE for our model
                //Console.Write("\t" + p.name + "...\t");
                if (p.game_stats.Keys.Select(g => g.season).Distinct().Count() > 2)
                {
                    double std_error = p.GenerateModel(teams, update_info);
                    std_errors += std_error;
                    count++;
                    //Console.WriteLine(std_error);
                }
                else
                {
                    //Console.WriteLine("X");
                }
            }
            std_errors /= count;

            Console.WriteLine("\n\nNumber of tournaments: {0}\n" +
                "Number of games: {1}\n" +
                "Number of players: {2}\n" +
                "Number of teams: {3}\n" +
                "Average standard error: {4}\n\n",
                tournaments.Length, games.Length, players.Length, teams.Length, std_errors);

            Console.WriteLine("Testing on tournaments from past 3 seasons (This could take a while)...\t");
            double my_avg_error = 0;
            double avg_error_from_true = 0;
            double avg_best_submitted_error = 0;
            double my_avg_score = 0;
            double avg_best_value = 0;
            double avg_perfect_value = 0;
            double avg_submitted_value = 0;
            double avg_stdev = 0;
            double valid_tourneys = 0;
            double wins = 0;

            bool testing = false; // Set testing to TRUE if we don't want to run all this code
            count = 0;
            foreach (Tournament t in tournaments)
            {
                count++;
                Lineup lineup_solution = null;
                if (!testing)
                {
                    lineup_solution = FindLineupSolution(t, players, teams);
                }

                if (lineup_solution != null & count > 500)
                {
                    wins = lineup_solution.true_score > t.average_fp + (t.stdev_fp * 1.5) ? wins + 1 : wins;
                    my_avg_error += t.perfect_lineup_fp - lineup_solution.true_score;
                    my_avg_score += lineup_solution.true_score;
                    avg_error_from_true += Math.Abs(lineup_solution.pred_score - lineup_solution.true_score);

                    avg_best_submitted_error += t.perfect_lineup_fp - t.best_submitted_fp;

                    avg_best_value += t.best_submitted_fp;
                    avg_perfect_value += t.perfect_lineup_fp;

                    avg_submitted_value += t.average_fp;
                    avg_stdev += t.stdev_fp;

                    valid_tourneys++;
                }
            }

            // Turn sums into averages
            avg_perfect_value /= valid_tourneys;
            avg_best_value /= valid_tourneys;
            my_avg_score /= valid_tourneys;
            avg_submitted_value /= valid_tourneys;
            avg_stdev /= valid_tourneys;
            avg_error_from_true /= valid_tourneys;
            my_avg_error /= valid_tourneys;
            avg_best_submitted_error /= valid_tourneys;

            Console.WriteLine(
                "Tournaments analyzed: {0}\n\n" +
                "AVERAGES:\n" +
                "Perfect score: {1}\n" +
                "Best submitted score: {2}\n" +
                "My score: {3}\n" +
                "Mean score: {4}\n" +
                "Standard deviation: {5}\n\n" +
                "My error from the true value: {6}\n" +
                "My error from perfect: {7}\n" +
                "Best submitted error: {8}\n" +
                "Win percentage: {9}%\n",
                valid_tourneys,
                avg_perfect_value, avg_best_value, my_avg_score, avg_submitted_value, avg_stdev,
                avg_error_from_true, my_avg_error, avg_best_submitted_error, wins * 100 / valid_tourneys);


            // Now that we've tested it, run the program on an actual contest
            //Lineup lineup = FindLineupSolution(contest_rows, players, teams);
            //Console.WriteLine(lineup.pred_score + "\t" + lineup.cost);
            //foreach (Player p in lineup.lineup)
            //{
            //    Console.WriteLine(p.name + "\t" + p.position);
            //}


            Console.WriteLine(errors.Count + " errors.");
            File.WriteAllLines("error_log.txt", errors.ToArray());
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        private static Tournament[] GetTournaments(DateTime dt)
        {
            /*
             * Every day has a number of tournaments (not always nba)
             * Each tournament contains a nonzero number of games
             * Each game has stats
             * 
             * We'll look at each day, for each nba tournament that day, for each game in that tournament
             */

            // If file doesn't exist, write one
            if (!File.Exists(Directory.GetCurrentDirectory() + "/Data/Optimal Lineups.csv"))
            {
                List<string> lines = new List<string>();
                while (dt < DateTime.Today)
                {
                    lines.AddRange(GetTournamentData(dt));
                    dt = dt.AddDays(1);
                }

                // Write data to CSV
                File.WriteAllLines(Directory.GetCurrentDirectory() + "/Data/Optimal Lineups.csv", lines);
            }

            // Once the file does exist and has data, read it into a list
            string[] read_lines = File.ReadLines(Directory.GetCurrentDirectory() + "/Data/Optimal Lineups.csv").ToArray();
            List<Tournament> tournaments = new List<Tournament>();
            foreach (string line in read_lines)
            {
                if (line.Length > 0)
                {
                    string[] data = line.Split(',');
                    tournaments.Add(new Tournament(data[0], new DateTime(Int64.Parse(data[1])),
                        data[2], data[3], data[4], data[5], data[6], data[7]));
                }
            }

            // Make sure all data is up to date
            dt = tournaments.Last().datetime.AddDays(1);
            while (dt < DateTime.Today)
            {
                foreach (string line in GetTournamentData(dt))
                {
                    // Read in data into list
                    string[] data = line.Split(',');
                    tournaments.Add(new Tournament(data[0], new DateTime(Convert.ToInt64(data[1])),
                        data[2], data[3], data[4], data[5], data[6], data[7]));

                    // Append this data to the file
                    using (StreamWriter sw = File.AppendText(Directory.GetCurrentDirectory() + "/Data/Optimal Lineups.csv"))
                    {
                        sw.WriteLine(line);
                    }
                }
                dt = dt.AddDays(1);
            }

            return tournaments.ToArray();
        }

        private static string[] GetTournamentData(DateTime dt)
        {
            Console.WriteLine("Reading data from " + dt.ToShortDateString() + " from Yahoo!");
            string base_url_dt = "https://dfyql-ro.sports.yahoo.com/v2/contestSeries?lang=en-US&region=US&device=desktop&state=completed&startTimeMin=";
            string base_url_tournament = "https://dfyql-ro.sports.yahoo.com/v2/optimalLineup/";
            List<string> tournament_lines = new List<string>();
            List<string> game_lines = new List<string>();

            long min = Helper.ConvertToTimestamp(dt.ToUniversalTime());
            long max = Helper.ConvertToTimestamp(dt.AddDays(1).ToUniversalTime());
            string dt_url = base_url_dt + min + "000&startTimeMax=" + max + "000";
            string dt_source = Helper.GetSourceCode(dt_url);

            // Find chunk of source code where list of tournaments actually is
            string tournament_list_chunk = Helper.FindBetween(dt_source, "\"sports\"", "error");

            // For each index where there is a tournament index
            int[] tournament_indexes = Helper.GetAllIndexes(tournament_list_chunk, "\"id\":").ToArray();
            foreach (int tournament_index in tournament_indexes)
            {
                // Get the tournament data as a string and add it to the list
                string tournament_id = Helper.FindBetween(tournament_list_chunk, ":", ",", tournament_index);
                string tournament_url = base_url_tournament + tournament_id;
                string tournament_source = Helper.GetSourceCode(tournament_url);
                if (tournament_source.Length == 0) // Sometimes we can't get the webpage. If that happens, just skip to the next one
                {
                    Console.WriteLine("\tSkipped" + tournament_url);
                    continue;
                }
                string perfect_lineup_salary = Helper.FindBetween(tournament_source, "\"totalSalary\":", ",");
                string perfect_lineup_fp = Helper.FindBetween(tournament_source, "\"score\":", "}");
                string best_submitted_salary = Helper.FindBetween(tournament_source, "\"totalSalary\":", ",", tournament_source.IndexOf("allUserBestLineup"));
                string best_submitted_fp = Helper.FindBetween(tournament_source, "\"score\":", "}", tournament_source.IndexOf("allUserBestLineup"));

                //The order of these two is sometimes flipped so go to the comma and remove any }
                string average_fp = Helper.FindBetween(tournament_source, "\"meanScore\":", ",").TrimEnd('}');
                string stdev_fp = Helper.FindBetween(tournament_source, "\"scoreStdDev\":", ",").TrimEnd('}');

                tournament_lines.Add(tournament_id + "," + dt.Ticks + "," +
                                perfect_lineup_salary + "," + perfect_lineup_fp + "," +
                                best_submitted_salary + "," + best_submitted_fp + "," +
                                average_fp + "," + stdev_fp);

                // While we're here, read all the games involved in this tournament
                string game_code_list = Helper.FindBetween(tournament_list_chunk, "gameCodeList\":[\"", "]", tournament_index);

                Console.WriteLine("\t" + tournament_id + " - " + game_code_list.Split(',').Length + " games.");

                for (int i = 0; i < game_code_list.Split(',').Length; i++)
                {
                    string game_code = game_code_list.Split(',')[i];
                    int gc_index = dt_source.IndexOf("\"gameId\":\"" + game_code.Trim('\"'));
                    string box_url = Helper.FindBetween(dt_source, "\"boxscoreLink\":\"", "\",", gc_index);
                    if (box_url.Contains("nba"))
                    {
                        string box_score_id = box_url.Substring(box_url.LastIndexOf('-') + 1, 10);
                        string game_data_url = "https://sports.yahoo.com/site/api/resource/sports.game.latestResults;id=nba.g." + box_score_id + ";";
                        try
                        {
                            string game_data = GetGameData(box_score_id, tournament_id, game_data_url);
                            game_lines.Add(game_data);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            Console.WriteLine(game_data_url);
                            Console.WriteLine("Waiting 30 seconds to see if there was an issue with the connection.");
                            System.Threading.Thread.Sleep(30000);
                            i--;
                        }
                    }
                }
            }

            // Write all games this day to file
            foreach (string line in game_lines)
            {
                using (StreamWriter sw = File.AppendText(Directory.GetCurrentDirectory() + "/Data/Games.csv"))
                {
                    sw.WriteLine(line);
                }
            }

            // Return the tournament lines for this day
            return tournament_lines.ToArray();
        }

        private static string GetGameData(string box_score_id, string tournament_id, string url)
        {
            string source = Helper.GetSourceCode(url);
            string game_section = Helper.FindBetween(source, "\"gameid\":\"nba.g." + box_score_id + "\"", "\"if_necessary\":[");

            // Read in game data from tournament
            string gameid = box_score_id;
            string global_game_id = Helper.FindBetween(game_section, "\"global_gameid\":\"", "\"");
            string home_team_id = Helper.FindBetween(game_section, "\"home_team_id\":\"", "\"");
            string away_team_id = Helper.FindBetween(game_section, "\"away_team_id\":\"", "\"");
            string box_score_url = "https://sports.yahoo.com" + Helper.FindBetween(game_section, "\"url\":\"", "\"", game_section.IndexOf("url") + 1);
            string datetime = Helper.FindBetween(game_section, "\"start_time\":\"", " +");

            // Return a CSV string
            return gameid + "," + global_game_id + "," + home_team_id + "," + away_team_id + "," + box_score_url + "," + datetime + "," + tournament_id;
        }

        private static Game[] ReadGames(Tournament[] tournaments)
        {
            List<Game> games = new List<Game>();
            string[] lines = File.ReadAllLines("./Data/Games.csv");
            Tournament t = null;

            foreach (string line in lines)
            {
                string[] cols = line.Split(',');

                // Get tournament associated with this game
                if (t == null || !t.id.Equals(cols[7]))
                {
                    t = tournaments.Where(tourn => tourn.id.Equals(cols[7])).First();
                }
                games.Add(new Game(cols[0], cols[1], cols[2], cols[3], cols[4], cols[5] + " " + cols[6], t));
            }

            return games.ToArray();
        }

        private static Player[] GetPlayers(bool update, Game[] games)
        {
            List<Player> players = new List<Player>();

            if (update)
            {
                string all_teams_url = "https://sports.yahoo.com/nba/players/";
                string all_teams_source = Helper.GetSourceCode(all_teams_url);
                all_teams_source = Helper.FindBetween(all_teams_source, ">NBA Players<", "Latest Activity");

                int[] team_url_indexes = Helper.GetAllIndexes(all_teams_source, "<a href=\"/nba/teams/").ToArray();

                // For each team
                foreach (int team_url_index in team_url_indexes)
                {
                    string team_loc = Helper.FindBetween(all_teams_source, "/nba/teams/", "/", team_url_index - 1);
                    string team_source = Helper.GetSourceCode("https://sports.yahoo.com/nba/teams/" + team_loc + "/stats");
                    string team_id = Helper.FindBetween(team_source, "teamId=", "&");
                    team_source = Helper.FindBetween(team_source, ">Player<", "</table>");

                    // Get positions
                    string pos_source = Helper.GetSourceCode("https://sports.yahoo.com/site/api/resource/sports.team.roster;id=" + team_id);

                    // Find the number associated with each position in Yahoo!
                    string[] position_number_lookup = new string[]
                    {
                        "PG", "SG", "G", "GF", "SF", "PF", "F", "FC", "C"
                    };

                    // Find primary position of each player by ID
                    string player_positions_source = Helper.FindBetween(pos_source, "playerprimary_position_id", "\":{");
                    string pos_5_source = Helper.FindBetween(pos_source, "\"depth_chart\"", "},");

                    // Lookup which player_id was which position
                    Dictionary<string, string> position_lookup = new Dictionary<string, string>();
                    string[] sections = player_positions_source.Split(',');
                    foreach (string player_section in sections.Take(sections.Length - 2))
                    {
                        string player_id = Helper.FindBetween(player_section, "nba.p.", "\"");
                        int position_index = Int32.Parse(Helper.FindBetween(player_section, "nba.pos.", "\"")) - 1;

                        // If players are given a G, GF, F, or FC, clear up which it is
                        if (new int[] { 2, 3, 6, 7 }.Contains(position_index))
                        {
                            int lowest_depth = -1;

                            // For each of the major 5 positions
                            string[] position_sections = pos_5_source.Split(']').Take(5).ToArray();
                            foreach (string position_section in position_sections)
                            {
                                // Temporary position_index
                                int pos_i = Int32.Parse(Helper.FindBetween(position_section, "nba.pos.", "\"")) - 1;

                                // Tighten the string to just between the brackets
                                string pos_sect = position_section.Substring(position_section.IndexOf('['));

                                // If lowest depth hasn't been set or their id has a lower index in player section, set new position
                                if (lowest_depth == -1 || (pos_sect.IndexOf(player_id) < lowest_depth && pos_sect.IndexOf(player_id) != -1))
                                {
                                    position_index = pos_i;
                                    lowest_depth = pos_sect.IndexOf(player_id);
                                }
                            }
                        }

                        // Add player and position if one of the 5 major positions
                        if (new int[] { 0, 1, 4, 5, 8 }.Contains(position_index))
                        {
                            position_lookup.Add(player_id, position_number_lookup[position_index]);
                        }
                    }


                    // For each player on this team, get their Player object
                    int[] player_id_indexes = Helper.GetAllIndexes(team_source, "/nba/players").ToArray();
                    for (int i = 0; i < player_id_indexes.Length; i++)
                    {
                        int player_id_index = player_id_indexes[i];
                        string player_id = Helper.FindBetween(team_source, "/nba/players/", "\"", player_id_index - 1);
                        string player_name = Helper.FindBetween(team_source, ">", "<", player_id_index - 1);

                        Console.Write(player_name + "...\t");
                        // Something is weird with this player if not in dictionary, just skip them
                        if (!position_lookup.Keys.Contains(player_id))
                        {
                            Console.WriteLine("X");
                            continue;
                        }

                        // Sometimes Yahoo! stops us from pulling data. Wait 30 seconds and try again
                        if (position_lookup.Keys.Contains(player_id) && !GetPlayerData(player_id, player_name, team_id, position_lookup[player_id]))
                        {
                            i--;
                            Console.WriteLine("X");
                            System.Threading.Thread.Sleep(30000);
                        }
                        else
                        {
                            Console.WriteLine("\u2713");
                        }
                    }
                }
            }

            // To improve speed, make dictionary <gameid, Game> to find game
            Dictionary<string, Game> gameid_game_dict = new Dictionary<string, Game>();
            foreach (Game g in games)
            {
                if (!gameid_game_dict.ContainsKey(g.gameid))
                {
                    gameid_game_dict.Add(g.gameid, g);
                }
            }

            // Read all Player data
            foreach (string filename in Directory.GetFiles("./Data/Player Games/"))
            {
                string[] lines = File.ReadAllLines(filename);
                string[] basic_player_data = lines[0].Split(',');
                if (basic_player_data.Length != 4)
                {
                    continue;
                }
                Player p = new Player(basic_player_data[0], basic_player_data[1], basic_player_data[2], basic_player_data[3]);

                foreach (string stat_line in lines.Skip(1))
                {
                    string[] split_stat_line = stat_line.Split(',');
                    if (gameid_game_dict.ContainsKey(split_stat_line[split_stat_line.Length - 2]))
                    {
                        Game game = gameid_game_dict[split_stat_line[split_stat_line.Length - 2]];
                        p.AddGameStats(game, stat_line);
                    }
                }
                players.Add(p);
            }


            return players.ToArray();
        }

        private static bool GetPlayerData(string player_id, string player_name, string team_id, string position)
        {
            // This is where we'll store each of the stat_lines
            List<string> game_stats = new List<string>();
            game_stats.Add(player_id + "," + player_name + "," + team_id + "," + position);

            // For each of the last 3 seasons
            foreach (int year in new int[] { 2019, 2018, 2017 })
            {
                string player_url = "https://graphite-secure.sports.yahoo.com/v1/query/shangrila/gameLogBasketball?lang=en-US&playerId=nba.p." + player_id + "&season=" + year;
                string player_source = Helper.GetSourceCode(player_url);

                // If we pull too much data too quickly, they block us for a sec. Return false if we fail
                if (player_source.Length == 0)
                {
                    return false;
                }
                string[] game_sources = player_source.Split(new string[] { "\"game\":" }, StringSplitOptions.None).Skip(1).ToArray();

                // For each game in the last 3 seasons
                foreach (string game_source in game_sources)
                {
                    // List of game_stats
                    string stat_line = "";
                    int[] stat_indexes = Helper.GetAllIndexes(game_source, "\"value\":").ToArray();

                    // For each stat in each game of each of the last 3 seasons
                    foreach (int stat_index in stat_indexes)
                    {
                        string stat = Helper.FindBetween(game_source, "\"", "\"", stat_index + 7);

                        // If the stat is null it won't necessarily find it.
                        if (stat.Contains("statId"))
                        {
                            stat = "0";
                        }

                        // Add this stat to the line
                        stat_line += stat + ",";
                    }

                    // Get gameid for connections later and add stat_line to list. If no gameid, don't use this game
                    if (game_source.Contains("\"url\":\""))
                    {
                        string gameid = Helper.FindBetween(game_source, "\"url\":\"", "\"}}");
                        gameid = gameid.Substring(gameid.LastIndexOf("-") + 1, 10);

                        // Get what team Player was on during this game
                        // Find where this game was in the whole source code, back up a few characters, and find the first teamId
                        string game_team_id = Helper.FindBetween(player_source, "teamId\":\"", "\"", player_source.IndexOf(game_source) - 50);
                        stat_line += gameid + "," + game_team_id;

                        game_stats.Add(stat_line);
                    }
                }
            }

            // Order from oldest to newest (skipping basic info line)
            game_stats.Reverse(1, game_stats.Count() - 1);

            // Write to a file
            File.WriteAllLines("./Data/Player Games/" + player_name + ".csv", game_stats);
            return true;
        }

        private static Team[] GetTeams(bool update, Player[] players)
        {
            List<Team> teams = new List<Team>();
            if (update)
            {
                int[] seasons = new int[] { 2017, 2018, 2019 };
                for (int i = 0; i < seasons.Length; i++)
                {
                    int season = seasons[i];
                    Console.WriteLine(season + "...\t");
                    List<string> write_lines = new List<string>();
                    string all_teams_source = Helper.GetSourceCode("https://sports.yahoo.com/nba/stats/team/?season=" + season);
                    string all_opp_source = Helper.GetSourceCode("https://sports.yahoo.com/nba/stats/team/?selectedTable=1&season=" + season);
                    string[] team_rows = all_teams_source.Split(new string[] { "<a href=\"https://sports.yahoo.com/nba/teams/" }, StringSplitOptions.None).Skip(1).ToArray();
                    string[] opp_rows = all_opp_source.Split(new string[] { "<a href=\"https://sports.yahoo.com/nba/teams/" }, StringSplitOptions.None).Skip(1).ToArray();
                    if (team_rows.Length == 0 || opp_rows.Length == 0)
                    {
                        i--;
                        System.Threading.Thread.Sleep(30000);
                    }
                    else
                    {
                        // For each row in the stats page, get the data and add it to the list
                        foreach (string team_row in team_rows)
                        {
                            string opp_row = opp_rows.Where(r => r.Contains(team_row.Substring(0, team_row.IndexOf("/")))).First();
                            write_lines.Add(GetTeamData(team_row, opp_row));
                        }

                        // Write the file to be read from later
                        File.WriteAllLines("./Data/Teams_" + season + ".csv", write_lines);
                    }
                }
            }

            // Read in data from file
            string[] lines_2019 = File.ReadAllLines("./Data/Teams_2019.csv");
            foreach (string line in lines_2019)
            {
                // Get team basic data
                string[] cols = line.Split(',');
                Team team = new Team(cols[cols.Length - 2], cols[cols.Length - 1], cols[0]);
                teams.Add(team);

                // Add team's stats
                string[] lines_2018 = File.ReadAllLines("./Data/Teams_2018.csv");
                string[] lines_2017 = File.ReadAllLines("./Data/Teams_2018.csv");

                team.AddRatings(ReadTeamSeasonStats(team, lines_2017), 2);
                team.AddRatings(ReadTeamSeasonStats(team, lines_2018), 1);
                team.AddRatings(ReadTeamSeasonStats(team, lines_2019), 0);
            }

            return teams.ToArray();
        }

        private static Dictionary<string, double> ReadTeamSeasonStats(Team team, string[] lines)
        {
            Dictionary<string, double> team_ratings = new Dictionary<string, double>();
            string[] stat_keys = new string[]
            {
                    "location",
                    "fgm", "fga", "fg%",
                    "3pm", "3pa", "3p%",
                    "ftm", "fta", "ft%",
                    "or", "dr", "reb",
                    "ast", "to", "stl", "blk", "pf", "pts",
                    "fgm_opp", "fga_opp", "fg%_opp",
                    "3pm_opp", "3pa_opp", "3p%_opp",
                    "ftm_opp", "fta_opp", "ft%_opp",
                    "or_opp", "dr_opp", "reb_opp",
                    "ast_opp", "to_opp", "stl_opp", "blk_opp", "pf_opp", "pts_opp",
                    "team_id", "name"
            };
            string line = lines.Where(s => s.Contains(team.team_id)).First();
            string[] cols = line.Split(',');

            // Skip first and last two cols
            for (int i = 1; i < cols.Length - 2; i++)
            {
                team_ratings.Add(stat_keys[i], Double.Parse(cols[i]));
            }

            return team_ratings;
        }

        private static string GetTeamData(string team_row, string opp_row)
        {
            string loc = team_row.Substring(0, team_row.IndexOf("/")); // Get team location
            string line = loc;

            // For each column in the row, get the data and add it to the line
            int[] column_indexes = Helper.GetAllIndexes(team_row, "span data-reactid").Take(18).ToArray();
            foreach (int col_index in column_indexes)
            {
                line += "," + Helper.FindBetween(team_row, ">", "<", col_index);
            }

            column_indexes = Helper.GetAllIndexes(opp_row, "span data-reactid").Take(18).ToArray();
            foreach (int col_index in column_indexes)
            {
                line += "," + Helper.FindBetween(opp_row, ">", "<", col_index);
            }

            // Go to the team page and get their basic data
            string team_page_source = Helper.GetSourceCode("https://sports.yahoo.com/nba/teams/" + loc + "/");
            string team_id = Helper.FindBetween(team_page_source, "teamId=", "&");
            string team_name = Helper.FindBetween(team_page_source, "<title>", " on Yahoo! Sports");
            line += "," + team_id + "," + team_name;

            return line;
        }

        private static void AssignSalaries(string[] rows, Player[] players)
        {
            // Read in known salaries
            List<SalaryObject> salary_objects = new List<SalaryObject>();
            foreach (string row in rows.Skip(1))
            {
                string[] cols = row.Split(',');
                SalaryObject so = new SalaryObject
                {
                    SeasonAvg = float.Parse(cols[9]),
                    Salary = float.Parse(cols[8])
                };

                salary_objects.Add(so);
            }

            // Instantiate mlContext
            MLContext mlContext = new MLContext(seed: 0);
            IDataView salaries_dataview = mlContext.Data.LoadFromEnumerable<SalaryObject>(salary_objects);

            // Create schema
            IEstimator<ITransformer> pipeline =
                mlContext.Transforms.CopyColumns(outputColumnName: "ProjSalary", inputColumnName: "Salary")
                .Append(mlContext.Transforms.Concatenate("Features", "SeasonAvg"))
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.Regression.Trainers.FastTree(labelColumnName: "ProjSalary", featureColumnName: "Features"));

            // Generate regression model
            ITransformer model = pipeline.Fit(salaries_dataview);

            // Generate prediction model
            PredictionEngine<SalaryObject, ProjSalaryObject> predictionFunction = mlContext.Model.CreatePredictionEngine<SalaryObject, ProjSalaryObject>(model);


            // Predict salaries
            foreach (Player player in players)
            {
                foreach (Game game in player.game_stats.Keys)
                {
                    int proj_salary = 14; // Set as average in case 0 season games

                    // Get season average as input
                    double season_avg_fp = 0;
                    Game[] season_games = player.game_stats.Keys.Where(g => g.season == game.season && g.datetime < game.datetime).ToArray();

                    // If there were games played this season, we can predict their salaries
                    if (season_games.Length > 0)
                    {
                        foreach (Game g in season_games)
                            season_avg_fp += player.game_stats[g].Last();
                        season_avg_fp /= season_games.Length;

                        SalaryObject so = new SalaryObject
                        {
                            SeasonAvg = (float)season_avg_fp,
                            Salary = 0
                        };
                        proj_salary = (int)Math.Round(predictionFunction.Predict(so).Salary);
                    }

                    // Add salary to lookup
                    player.salary_lookup.Add(game, proj_salary);
                }
            }
        }

        private static Lineup FindLineupSolution(Tournament tournament, Player[] all_players, Team[] all_teams)
        {
            // Dictionary of all valid lineups to put in file
            List<Lineup> valid_lineups = new List<Lineup>();

            // Get players and their stats for this tournament
            DateTime[] active_game_dts = tournament.games.Select(g => g.datetime).ToArray();

            // Get which players were active for this tournament
            List<Player> active_players = all_players.Where(p => p.game_stats.Keys.Select(g => g.datetime).Intersect(active_game_dts).Any()).ToList();
            if (active_players.Count() == 0)
            {
                Console.WriteLine("No valid players!");
                return null;
            }

            // Project fp for each player in this tournament
            List<TPNode> tp_nodes = new List<TPNode>();
            foreach (Player p in active_players)
            {
                if (p.model != null)
                {
                    tp_nodes.Add(new TPNode(p, active_game_dts, all_teams));
                }
            }

            // Sort list by proj_fp small -> big
            tp_nodes.Sort();

            // Get max and min subset sums
            int low = tp_nodes.GetRange(0, 8).Sum(x => x.proj_fp);
            int high = tp_nodes.GetRange(tp_nodes.Count() - 8, 8).Sum(x => x.proj_fp);

            // Generate dp[,,]
            // Perfect Subset Sum Problem with 3rd dimension of num_items = k
            TPGraph tp_graph = new TPGraph(tp_nodes.Count(), (high + 1), 9, tp_nodes, tournament);
            Console.WriteLine("Generated graph: (" + tp_nodes.Count() + ", " + (high + 1) + ", " + 9 + ")");
            Console.WriteLine("\tNodes: " + tp_graph.nodes + "\tEdges: " + tp_graph.edges + "\tPaths to zero: " + tp_graph.paths);

            int sum = high;
            // Find all possible lineups for each sum up to 50 lineups
            for (sum = high; sum >= low && valid_lineups.Count() < 50; sum--)
            {
                valid_lineups.AddRange(tp_graph.FindLineups(sum));
            }
            Console.WriteLine("\tFrom " + high + " to " + sum);

            // Put all valid lineups in a file
            valid_lineups = valid_lineups.OrderByDescending(l => l.pred_score).ToList();
            List<string> lines = new List<string>();
            foreach (Lineup lineup in valid_lineups.Take(100))
            {
                // First line is the lineup's projected score and total cost
                lines.Add(lineup.pred_score + "," + lineup.cost);
                for (int i = 0; i < lineup.lineup.Length; i++)
                {
                    // Subsequent lines are the player's id, name, position, and salary for that game
                    Player p = lineup.lineup[i];
                    lines.Add(p.player_id + "," + p.name + "," + p.position + "," + lineup.salary_lookup[i]);
                }
                lines.Add("");
            }

            File.WriteAllLines("./Data/Valid Lineups/" + tournament.id + ".csv", lines);
            return valid_lineups.First();
        }
    }

    internal class TPGraph
    {
        private TPNode[] sum_heads { get; }
        private Lineup path { get; set; }
        public List<Lineup> all_paths { get; }
        private Tournament tournament { get; }
        public int edges { get; set; }
        public int nodes { get; set; }
        public int paths { get; set; }
        public TPGraph(int i, int j, int k, List<TPNode> tp_nodes, Tournament tournament)
        {
            this.edges = 0;
            this.nodes = 0;
            this.paths = 0;

            // This is the matrix behind the graph. We need it for the heads for each sum
            TPNode[,,] tpnode_matrix = GenerateMatrix(i, j, k, tp_nodes);

            // Each head for any sum we could want to make
            this.sum_heads = new TPNode[tpnode_matrix.GetLength(1)];
            for (int col = 0; col < sum_heads.Length; col++)
            {
                this.sum_heads[col] = tpnode_matrix[i - 1, col, k - 1];
            }

            // This stores all paths that we will return
            this.all_paths = new List<Lineup>();

            // This stores the current path for recursive tracing
            this.path = new Lineup(this.tournament);

            this.tournament = tournament;
        }

        public List<Lineup> FindLineups(int sum)
        {
            // Clear out previous paths
            this.path = new Lineup(this.tournament);
            this.all_paths.Clear();

            if (this.sum_heads[sum] != null)
            {
                // Start recursive tracing with this sum's head Node
                this.sum_heads[sum].TracePath(this.path, this.all_paths);
            }

            return this.all_paths;
        }

        public TPNode[,,] GenerateMatrix(int i, int j, int k, List<TPNode> tp_nodes)
        {
            TPNode[,,] tpnode_matrix = new TPNode[i, j, k];

            // A depth of 0 is only possible for trying to make a sum of 0
            for (int row = 0; row < tpnode_matrix.GetLength(0); row++)
            {
                tpnode_matrix[row, 0, 0] = new TPNode(tp_nodes[row], null, null);
                this.nodes++;
            }

            // Using just the first element is only possible with exactly 1 elements for its value
            tpnode_matrix[0, tp_nodes[0].proj_fp, 1] = new TPNode(tp_nodes[0], tpnode_matrix[0, 0, 0], null);
            this.nodes++;
            this.edges++;

            for (int row = 1; row < tpnode_matrix.GetLength(0); row++)
            {
                for (int col = 0; col < tpnode_matrix.GetLength(1); col++)
                {
                    for (int depth = 1; depth < tpnode_matrix.GetLength(2); depth++)
                    {
                        // Values to be found
                        TPNode include_this = null;
                        TPNode dont_include_this = null;

                        // If we have to use every element, see if we can by including this Node3D
                        if (depth == row + 1)
                        {
                            // Test including this Node if there is space to do so
                            if (col >= tp_nodes[row].proj_fp)
                            {
                                include_this = tpnode_matrix[row - 1, col - tp_nodes[row].proj_fp, depth - 1];
                            }
                        }

                        // If we more than depth elements are at our disposal, see if we include or don't include
                        else if (depth < row + 1)
                        {
                            // Test including this Node if there is space to do so
                            if (col >= tp_nodes[row].proj_fp)
                            {
                                include_this = tpnode_matrix[row - 1, col - tp_nodes[row].proj_fp, depth - 1];
                            }

                            dont_include_this = tpnode_matrix[row - 1, col, depth];
                        }

                        // Add this Node with the values we found
                        if (include_this != null || dont_include_this != null)
                        {
                            tpnode_matrix[row, col, depth] = new TPNode(tp_nodes[row], include_this, dont_include_this);
                            this.nodes++;

                            if (include_this != null)
                            {
                                this.edges++;
                            }

                            if (dont_include_this != null)
                            {
                                this.edges++;
                            }

                            if (include_this != null && dont_include_this != null)
                            {
                                this.paths++;
                            }
                        }
                    }
                }
            }

            return tpnode_matrix;
        }
    }

    internal class TPNode : IComparable<TPNode>
    {
        // Stands for 'TEMPORARY PLAYER'
        public Player player { get; }
        public int proj_fp { get; }
        public Game game { get; }
        public int salary { get; }
        public TPNode include_this { get; }
        public TPNode dont_include_this { get; }

        public TPNode(Player p, DateTime[] active_game_dts, Team[] all_teams)
        {
            DateTime game_dt = p.game_stats.Keys.Select(g => g.datetime).Intersect(active_game_dts).First();
            this.player = p;
            this.game = p.game_stats.Keys.Where(g => g.datetime.Equals(game_dt)).First();
            this.proj_fp = p.ProjectFP(this.game, all_teams);
            this.salary = p.salary_lookup[this.game];
            this.include_this = null;
            this.dont_include_this = null;
        }

        public TPNode(TPNode tpn, TPNode include_this, TPNode dont_include_this)
        {
            this.player = tpn.player;
            this.proj_fp = tpn.proj_fp;
            this.game = tpn.game;
            this.salary = tpn.salary;
            this.include_this = include_this;
            this.dont_include_this = dont_include_this;
        }

        public void TracePath(Lineup path, List<Lineup> all_paths)
        {
            // Check if the path is done
            if (path.IsValid())
            {
                all_paths.Add(path);
            }

            // Check that we haven't already gone over the salary cap
            if (path.cost > 200)
            {
                return;
            }

            // If we are at an end Node, there wont be any pointers so no more paths will branch off

            // Follow path by not including this TPNode
            if (dont_include_this != null)
            {
                // Only need to branch off path if there is an include_this
                if (include_this != null)
                {
                    Lineup dont_include_path = new Lineup(path);
                    dont_include_this.TracePath(dont_include_path, all_paths);
                }
                else
                {
                    dont_include_this.TracePath(path, all_paths);
                }
            }

            // Follow path by including this TPNode
            if (include_this != null)
            {
                // Try to add this player
                int index = path.AddPlayer(this.player, this.game, this.proj_fp);

                // Check if they were added successfully before tracing the rest of that path
                if (index != -1)
                {
                    include_this.TracePath(path, all_paths);
                }
            }
        }

        public int CompareTo(TPNode other)
        {
            if (this.proj_fp == other.proj_fp)
            {
                // Return cheaper player first if proj_fp are the same
                return this.salary.CompareTo(other.salary);
            }
            // Return higher proj_fp first
            return this.proj_fp.CompareTo(other.proj_fp); //ASC is way WAY faster because our paths short out sooner
            return other.proj_fp.CompareTo(this.proj_fp); //DESC
        }

        public override string ToString()
        {
            return this.player.name;
        }
    }

    public class SalaryObject
    {
        [ColumnName("SeasonAvg")]
        public float SeasonAvg { get; set; }

        [ColumnName("Salary")]
        public float Salary { get; set; }
    }

    public class ProjSalaryObject
    {
        [ColumnName("Score")]
        public float Salary;
    }

}
