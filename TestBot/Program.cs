using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace TestBot
{
    class Program
    {
        static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();

        public static string prefix = "j!"; //Prefix to use for bot commands, eg j!initialize, %initialize, etc
        string token = "[PLACEHOLDER]"; //Get this fron the Discord Developers Portal, don't share it

        static bool bigText = true; //Display clue text size large or small
        static int defaultDelay = 4000; //How many miliseconds to wait for the question to finish, before adjusting for clue length. This is for early buzz, 4 seconds later triple stumpers kick in
        static int wordsForAnotherSecond = 3; //How many words are needed to add an extra second to the clue timer
        static int defaultTimeToRespond = 10; //How many seconds are allowed to respond
        static int defaultTimeToRespondDD = 15; //How many seconds are allowed to respond (before bonus time for "reading")
        static int defaultTimeToRespondFJ = 40; //How many seconds are allowed to respond for FJ (before bonus time for "reading")
        static bool lenient = false; //Allow more leniency in accepting responses (Aople for Apple, for example)
        static int lenienceExtent = 1; //The higher the number, the more lenient
        static int needsToBeExactLength = 4; //Answers up to this many characters must be exact, no leniency will be given
        static bool verifyIncorrectResponses = true; //If an incorrect response is given, prompt the host to accept, deny, or BMS it.


        static bool champSelectsFirst = false; //If set to true, the first player to join (red) will select first. If false, it'll be random.


        public static List<string> questionBoard = File.ReadLines("[INSERT PATH HERE]").ToList();




        // Don't mess with anything below this line unless you know what you're doing

        static Random rdm = new Random();

        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        public List<JeopardyGame> games = new List<JeopardyGame>();





        public class JeopardyGame
        {


            List<SocketUser> hosts = new List<SocketUser>();
            List<Player> players = new List<Player>();
            public ChannelData channelData;

            public List<Category> currentRound = new List<Category>();
            public List<Category> singleJ = new List<Category>();
            public List<Category> doubleJ = new List<Category>();
            public FinalJ finalJ;

            int timeToRespond = 0;
            int round = -1; //0 = Single J, 1 = Double J, 2 = Final J
            int categorySelected = -1;
            int playerInControl = 0;
            int valueSelected = -1;
            bool canBuzz = false;
            bool ddBeingPlayed = false;
            int ddWager = 0;
            int maxBet = 0;
            bool waitingForWager = false;
            bool finalJTimerUp = false;
            int playerThatBuzzedIn = -1;
            bool hasResponded = false;
            string response = "";
            string dailyDoubleLog = "# Daily Doubles:\n\n";
            List<int> playersAttemptingToBuzz = new List<int>();
            List<int> playersWhoHaveBuzzedIn = new List<int>();
            bool wasAccepted = false;


            bool finalJDidStart = false; //Debugging var

            #region Jeopardy

            public class Player
            {
                public SocketUser User { get; set; }
                public string Username { get; set; }
                public int Score { get; set; }
                public int Coryat { get; set; }
                public bool IsBot { get; set; }
                public int BotDifficulty { get; set; }
                public PlayerAdvancedStats Stats { get; set; }
                public string FinalJeopardyResponse { get; set; }
                public int FinalJeopardyWager { get; set; }
                public bool MadeFinalWager { get; set; }

                // Constructor to initialize the Player object
                public Player(SocketUser user, string username, bool isBot, int botDifficulty)
                {
                    User = user;
                    Username = username;
                    Score = 0;
                    Coryat = 0;
                    IsBot = isBot;
                    BotDifficulty = botDifficulty;
                    Stats = new PlayerAdvancedStats();
                    FinalJeopardyResponse = "(timed out)";
                    FinalJeopardyWager = 0;
                    MadeFinalWager = false;
                }
            }

            public class PlayerAdvancedStats
            {
                public int CorrectResponses { get; set; }
                public int IncorrectResponses { get; set; }
                public int Attempts { get; set; }
                public int Buzzes { get; set; }
                public int SingleJScore { get; set; }
                public int DoubleJScore { get; set; }
                public int SingleJAttempts { get; set; }
                public int SingleJBuzzes { get; set; }
                public int DoubleJAttempts { get; set; }
                public int DoubleJBuzzes { get; set; }

                public int SingleJCorrect { get; set; }
                public int DoubleJCorrect { get; set; }
                public int SingleJIncorrect { get; set; }
                public int DoubleJIncorrect { get; set; }

                // Constructor to initialize the Player object
                public PlayerAdvancedStats()
                {
                    CorrectResponses = 0;
                    IncorrectResponses = 0;
                    Attempts = 0;
                    Buzzes = 0;
                    SingleJScore = 0;
                    DoubleJScore = 0;
                    SingleJAttempts = 0;
                    DoubleJAttempts = 0;
                    SingleJBuzzes = 0;
                    DoubleJBuzzes = 0;
                }
            }

            public class ChannelData
            {
                public ISocketMessageChannel Channel { get; set; }
                public IUserMessage Board { get; set; }
                public IUserMessage Clue { get; set; }
                public IUserMessage Image { get; set; }

                // Constructor to initialize the Player object
                public ChannelData(ISocketMessageChannel channel, IUserMessage board, IUserMessage clue, IUserMessage image)
                {
                    Channel = channel;
                    Board = board;
                    Clue = clue;
                    Image = image;
                }
            }

            public class Category
            {
                public string Name { get; set; }
                public string SpecialInstructions { get; set; }
                public Clue[] Clues { get; set; }

                // Constructor to initialize the Player object
                public Category(string name, string specialInstructions, Clue[] clues)
                {
                    Name = name;
                    SpecialInstructions = specialInstructions;
                    Clues = clues;
                }
            }

            public class Clue
            {
                public int Value { get; set; }
                public string Question { get; set; }
                public string[] Answers { get; set; }
                public string ImageLink { get; set; }
                public bool IsDD { get; set; }
                public bool Played { get; set; }


                // Constructor to initialize the Player object
                public Clue(int value, string question, string[] answers, string imageLink, bool isDD, bool played)
                {
                    Question = question;
                    Answers = answers;
                    ImageLink = imageLink;
                    IsDD = isDD;
                    Played = played;
                    Value = value;
                }
            }

            public class FinalJ
            {
                public string Category { get; set; }
                public string Question { get; set; }
                public string[] Answers { get; set; }
                public string ImageLink { get; set; }


                // Constructor to initialize the Player object
                public FinalJ(string category, string question, string[] answers, string imageLink)
                {
                    Category = category;
                    Question = question;
                    Answers = answers;
                    ImageLink = imageLink;
                }
            }



            public async Task InitializeGame(SocketCommandContext ctx)
            {
                questionBoard.RemoveAll(str => str.StartsWith("#"));
                questionBoard.RemoveAll(str => str == "");
                if (questionBoard.Count == 74)
                {
                    IUserMessage board, clue, image;
                    board = await ctx.Channel.SendMessageAsync(embed: JoinScreen().Build(), components: JoinScreenButtons().Build());
                    clue = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithDescription("`----------------`").
                        WithTitle("Clue").Build(),
                        components: ClueButtons(false).Build());
                    image = await ctx.Channel.SendMessageAsync("`----------------`");
                    channelData = new ChannelData(ctx.Channel, board, clue, image);

                    string[] questionDetails;
                    string[] correctAns;

                    for (int categoryOn = 0; categoryOn < 12; categoryOn++)
                    {
                        string categoryName = "";
                        string categoryInstructions = "";
                        Clue[] clues = new Clue[5];

                        string[] categoryDetails = questionBoard[categoryOn * 6].Replace("Category: ", "").Split("(");
                        categoryName = categoryDetails[0];
                        if (categoryDetails.Length > 1)
                        {
                            categoryInstructions = "(" + categoryDetails[1];
                        }
                        for (int i = 1; i < 6; i++)
                        {
                            questionDetails = questionBoard[categoryOn * 6 + i].Split("|");
                            correctAns = questionDetails[2].Split(",");
                            bool dd = false;
                            if (questionDetails[4] == "DD")
                            {
                                dd = true;
                            }


                            if (questionDetails[0] == "x")
                            {
                                clues[i - 1] = new Clue(0, questionDetails[1], correctAns, questionDetails[3],
                                dd, false);
                                clues[i - 1].Played = true;
                            }
                            else
                            {
                                clues[i - 1] = new Clue(int.Parse(questionDetails[0]), questionDetails[1], correctAns, questionDetails[3],
                                dd, false);
                            }
                        }
                        if (categoryOn < 6)
                        {
                            singleJ.Add(new Category(categoryName, categoryInstructions, clues));
                        }
                        else
                        {
                            doubleJ.Add(new Category(categoryName, categoryInstructions, clues));
                        }
                    }
                    questionDetails = questionBoard[73].Split("|");
                    correctAns = questionDetails[1].Split(",");
                    finalJ = new FinalJ(questionBoard[72].Replace("Category: ", ""), questionDetails[0], correctAns, questionDetails[2]);
                }
                else
                {
                    await ctx.Channel.SendMessageAsync("Seems something went wrong... Your board has **" + questionBoard.Count +
                        "** lines! It should have 74 total. Try checking the console to search for incorrect lines.");
                    for (int i = 0; i < questionBoard.Count; i++)
                    {
                        Console.WriteLine(i + ": " + questionBoard[i]);
                    }
                }
            }

            async Task StartGame(SocketInteractionContext ctx)
            {
                round = 0;
                if (!champSelectsFirst)
                {
                    playerInControl = rdm.Next(0, 3);
                }
                foreach (Category c in singleJ)
                {
                    currentRound.Add(c);
                }
                await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
            }

            bool CheckIfSwitch()
            {
                bool allCluesPlayed = true;
                foreach (Category c in currentRound)
                {
                    if (c.Clues.Any(c => c.Played == false))
                    {
                        allCluesPlayed = false;
                        break;
                    }
                }

                if (allCluesPlayed)
                {
                    SwitchRound();
                    return true;
                }
                else
                {
                    return false;
                }
            }

            void SwitchRound()
            {
                if (round == 1)
                {
                    playerInControl = players.IndexOf(players.OrderByDescending(p => p.Score).ElementAt(2));
                }
                currentRound.Clear();
                if (round == 0)
                {
                    round = 1;
                    foreach (Player p in players)
                    {
                        p.Stats.SingleJAttempts = p.Stats.Attempts;
                        p.Stats.SingleJCorrect = p.Stats.CorrectResponses;
                        p.Stats.SingleJIncorrect = p.Stats.IncorrectResponses;
                        p.Stats.SingleJBuzzes = p.Stats.Buzzes;
                        p.Stats.SingleJScore = p.Score;
                    }
                    foreach (Category c in doubleJ)
                    {
                        currentRound.Add(c);
                    }
                }
                else if (round == 1)
                {
                    foreach (Player p in players)
                    {
                        p.Stats.DoubleJAttempts = p.Stats.Attempts - p.Stats.SingleJAttempts;
                        p.Stats.DoubleJCorrect = p.Stats.CorrectResponses - p.Stats.SingleJCorrect;
                        p.Stats.DoubleJIncorrect = p.Stats.IncorrectResponses - p.Stats.SingleJIncorrect;
                        p.Stats.DoubleJBuzzes = p.Stats.Buzzes - p.Stats.SingleJBuzzes;
                        p.Stats.DoubleJScore = p.Score;
                    }
                    round = 2;
                    FinalJeopardyInitialize();
                }
            }

            void FinalJeopardyInitialize()
            {
                foreach (Player p in players)
                {
                    if (p.Score <= 0)
                    {
                        p.MadeFinalWager = true;
                    }
                    else if (p.IsBot)
                    {
                        p.MadeFinalWager = true;
                        switch (p.BotDifficulty)
                        {
                            case 1:
                                if (rdm.Next(0, 100) >= 66)
                                {
                                    p.FinalJeopardyResponse = finalJ.Answers[0];
                                }
                                break;
                            case 2:
                                if (rdm.Next(0, 100) >= 50)
                                {
                                    p.FinalJeopardyResponse = finalJ.Answers[0];
                                }
                                break;
                            case 3:
                                if (rdm.Next(0, 100) >= 33)
                                {
                                    p.FinalJeopardyResponse = finalJ.Answers[0];
                                }
                                break;
                            case 4:
                                if (rdm.Next(0, 100) >= 20)
                                {
                                    p.FinalJeopardyResponse = finalJ.Answers[0];
                                }
                                break;
                        }
                    }
                }
            }

            EmbedBuilder BoardEmbed()
            {
                var builder = new EmbedBuilder();
                string desc = "";

                if (round < 2)
                {
                    desc += ":black_large_square: :regional_indicator_a: :regional_indicator_b: :regional_indicator_c: " +
                    ":regional_indicator_d: :regional_indicator_e: :regional_indicator_f:\n";
                    string[] nums = { ":one:", ":two:", ":three:", ":four:", ":five:" };
                    string[] letters = { "a:", "b:", "c:", "d:", "e:", "f:" };
                    if (round == 0)
                    {
                        builder.WithTitle("Single Jeopardy");
                    }
                    else
                    {
                        builder.WithTitle("Double Jeopardy");
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        desc += nums[i] + " ";
                        for (int i2 = 0; i2 < 6; i2++)
                        {
                            if (currentRound[i2].Clues[i].Played == false)
                            {
                                desc += ":blue_square: ";
                            }
                            else if (currentRound[i2].Clues[i].Played == true && currentRound[i2].Clues[i].IsDD == true)
                            {
                                desc += ":red_square: ";
                            }
                            else
                            {
                                desc += ":black_large_square: ";
                            }
                        }
                        desc += "\n";
                    }
                    desc += "\n";
                    for (int i = 0; i < 6; i++)
                    {
                        if (categorySelected == i)
                        {
                            desc += ":regional_indicator_" + letters[i] + " **" + currentRound[i].Name + currentRound[i].SpecialInstructions + "**\n";
                        }
                        else
                        {
                            desc += ":regional_indicator_" + letters[i] + " " + currentRound[i].Name + currentRound[i].SpecialInstructions + "\n";
                        }

                    }
                }
                else
                {
                    builder.WithTitle("Final Jeopardy");
                    if (players.Any(p => p.MadeFinalWager == false))
                    {
                        desc += "# Category: " + finalJ.Category;
                        builder.WithFooter("Please make your wager. The final clue will be displayed as soon " +
                            "as all players do.");
                    }
                }
                string[] circles = { ":red_circle:", ":yellow_circle:", ":blue_circle:" };
                desc += "\n";
                for (int i = 0; i < 3; i++)
                {
                    if (!players[i].IsBot)
                    {
                        desc += circles[i] + " " + players[i].User.Mention + ": " + players[i].Score;
                        desc += "\n";
                    }
                    else
                    {
                        desc += circles[i] + " " + players[i].Username + ": " + players[i].Score;
                        desc += "\n";
                    }
                }

                builder.WithDescription(desc);
                if (round != 2)
                {
                    builder.WithFooter("" + players[playerInControl].Username + "'s selection");
                }
                builder.WithColor(Color.DarkBlue);
                return builder;
            }

            ComponentBuilder BoardCategoryComponents()
            {
                ComponentBuilder builder = new ComponentBuilder();
                if (round < 2)
                {
                    for (int i = 0; i < currentRound.Count; i++)
                    {
                        if (currentRound[i].Clues.Any(c => c.Played == false))
                        {
                            if (i < 3)
                            {
                                builder.WithButton("" + currentRound[i].Name, "Category" + i, disabled: false, row: 0);
                            }
                            else
                            {
                                builder.WithButton("" + currentRound[i].Name, "Category" + i, disabled: false, row: 1);
                            }
                        }
                        else
                        {
                            if (i < 3)
                            {
                                builder.WithButton("" + currentRound[i].Name, "Category" + i, disabled: true, row: 0);
                            }
                            else
                            {
                                builder.WithButton("" + currentRound[i].Name, "Category" + i, disabled: true, row: 1);
                            }
                        }
                    }
                    builder.WithButton("End Round", "endround", ButtonStyle.Danger, row: 2);
                }
                else
                {
                    if (players.All(p => p.MadeFinalWager == true))
                    {

                    }
                    else
                    {
                        builder.WithButton("Wager", "wager", ButtonStyle.Success);
                    }
                }
                builder.WithButton("Correct Scoring", "correctscoring", ButtonStyle.Secondary, row: 2);
                return builder;
            }

            ComponentBuilder BoardMoneyComponents()
            {
                ComponentBuilder builder = new ComponentBuilder();
                for (int i = 0; i < 5; i++)
                {
                    if (currentRound[categorySelected].Clues[i].Played == false)
                    {
                        builder.WithButton(currentRound[categorySelected].Clues[i].Value.ToString(), "Value" + i);
                    }
                }
                builder.WithButton("Correct Scoring", "correctscoring", ButtonStyle.Secondary, row: 1);
                return builder;
            }

            ComponentBuilder ScoringCorrectionComponents()
            {
                ComponentBuilder builder = new ComponentBuilder();
                builder.WithButton("Correct Scoring", "correctscoringfr", ButtonStyle.Primary);
                builder.WithButton("Back", "back", ButtonStyle.Primary);
                return builder;
            }

            EmbedBuilder JoinScreen()
            {
                var builder = new EmbedBuilder();
                builder.WithColor(Color.Blue);
                builder.WithTitle("Welcome to Jeopardy!");
                string description = "**Players:**\n";
                if (players.Count > 0)
                {
                    if (!players[0].IsBot)
                    {
                        description += ":red_circle: " + players[0].User.Mention + "\n";
                    }
                    else
                    {
                        description += ":red_circle: " + players[0].Username + "\n";
                    }
                }
                if (players.Count > 1)
                {
                    if (!players[1].IsBot)
                    {
                        description += ":yellow_circle: " + players[1].User.Mention + "\n";
                    }
                    else
                    {
                        description += ":yellow_circle: " + players[1].Username + "\n";
                    }
                }
                if (players.Count > 2)
                {
                    if (!players[2].IsBot)
                    {
                        description += ":blue_circle: " + players[2].User.Mention + "\n";
                    }
                    else
                    {
                        description += ":blue_circle: " + players[2].Username + "\n";
                    }
                }
                description += "\n**Hosts:**\n";
                foreach (SocketUser user in hosts)
                {
                    description += user.Mention + "\n";
                }

                builder.WithDescription(description);
                builder.WithFooter("Must have 3 players, no host limit");

                return builder;
            }

            ComponentBuilder JoinScreenButtons()
            {
                ComponentBuilder builder = new ComponentBuilder();
                builder.WithButton("Join as Player", "join", ButtonStyle.Success);
                builder.WithButton("Leave as Player", "leave", ButtonStyle.Danger);
                builder.WithButton("Join as Host", "host", ButtonStyle.Success);
                builder.WithButton("Leave as Host", "hostleave", ButtonStyle.Danger);
                builder.WithButton("Start Game", "start", ButtonStyle.Success);
                return builder;
            }

            async Task RoundEnded()
            {
                if (round == 1)
                {
                    playerInControl = players.IndexOf(players.OrderByDescending(p => p.Score).ElementAt(2));
                }
                await channelData.Board.ModifyAsync(c => c.Components = BoardCategoryComponents().Build());
                await channelData.Board.ModifyAsync(c => c.Embed = BoardEmbed().Build());
                if (round == 1)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        players[i].Stats.SingleJAttempts = players[i].Stats.Attempts;
                        players[i].Stats.SingleJBuzzes = players[i].Stats.Buzzes;
                        players[i].Stats.SingleJScore = players[i].Score;
                    }
                }
                if (round == 2)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        players[i].Stats.DoubleJAttempts = players[i].Stats.Attempts - players[i].Stats.SingleJAttempts;
                        players[i].Stats.DoubleJBuzzes = players[i].Stats.Buzzes - players[i].Stats.SingleJBuzzes;
                        players[i].Stats.DoubleJScore = players[i].Score;
                    }
                    await channelData.Clue.ModifyAsync(c => c.Components = new ComponentBuilder().Build());
                    await channelData.Clue.ModifyAsync(c => c.Embed = new EmbedBuilder().WithDescription("`----------------`").Build());
                    await channelData.Image.ModifyAsync(c => c.Content = "`----------------`");
                }
            }

            ModalBuilder ScoreCorrectModal()
            {
                ModalBuilder builder = new ModalBuilder();
                builder.WithCustomId("scorecorrect");
                builder.WithTitle("Correct Scoring");
                builder.AddTextInput("Player No", "playerno", placeholder: "0");
                builder.AddTextInput("Score", "score", placeholder: "0");
                builder.AddTextInput("Coryat", "coryat", placeholder: "0");
                builder.AddTextInput("Correct", "correct", placeholder: "0");
                builder.AddTextInput("Incorrect", "incorrect", placeholder: "0");
                return builder;
            }

            ModalBuilder WagerModal()
            {
                ModalBuilder builder = new ModalBuilder();
                builder.WithCustomId("wagermodal");
                builder.WithTitle("Make your Final Jeopardy Wager");
                builder.AddTextInput("Wager", "wager", placeholder: "0");
                return builder;
            }

            ModalBuilder FinalJModal()
            {
                ModalBuilder builder = new ModalBuilder();
                builder.WithCustomId("finaljmodal");
                builder.WithTitle("Final Jeopardy Response");
                builder.AddTextInput("Response", "response");
                return builder;
            }

            ComponentBuilder ClueButtons(bool menu)
            {
                ComponentBuilder builder = new ComponentBuilder();
                builder.WithButton("BUZZ", "buzz", ButtonStyle.Danger);
                if (menu)
                {
                    builder.WithButton("Accept", "accept", ButtonStyle.Success);
                    builder.WithButton("Deny", "deny", ButtonStyle.Danger);
                    builder.WithButton("BMS", "bms", ButtonStyle.Primary);
                }
                return builder;
            }

            EmbedBuilder DisplayClue(int type = 0) //0 = display, 1 = triple stumper, 2 = right, 3 = wrong, 4 = buzz, 5 = timeout,
                                                   //6 incorrect menu, 7 daily double, 8 == final, 9 = BMS
            {
                EmbedBuilder builder = new EmbedBuilder();
                string desc = "";
                if (type != 8)
                {
                    if (ddBeingPlayed && type <= 3)
                    {
                        builder.WithTitle(currentRound[categorySelected].Name + " (At stake: " + ddWager + ")");
                    }
                    else
                    {
                        builder.WithTitle(currentRound[categorySelected].Name + " " + currentRound[categorySelected].Clues[valueSelected].Value);
                    }
                    if (type != 7)
                    {
                        if (bigText)
                        {
                            desc = "# " + currentRound[categorySelected].Clues[valueSelected].Question;
                        }
                        else
                        {
                            desc = currentRound[categorySelected].Clues[valueSelected].Question;
                        }

                        switch (type)
                        {
                            case 0:
                                builder.WithColor(Color.Blue);
                                break;
                            case 1:
                                builder.WithColor(Color.DarkerGrey);
                                desc += "\nTriple Stumper! (Correct answer(s): ";
                                foreach (string s in currentRound[categorySelected].Clues[valueSelected].Answers)
                                {
                                    desc += s + ", ";
                                }
                                desc = desc.Remove(desc.Length - 2) + ")";
                                break;
                            case 2:
                                builder.WithColor(Color.Green);
                                if (ddBeingPlayed)
                                {
                                    desc += "\n" + players[playerInControl].Username + " correctly answered with \"" + response + "\" (Correct" +
                                        " answer(s): ";
                                }
                                else
                                {
                                    desc += "\n" + players[playerThatBuzzedIn].Username + " correctly answered with \"" + response + "\" (Correct" +
                                        " answer(s): ";
                                }
                                foreach (string s in currentRound[categorySelected].Clues[valueSelected].Answers)
                                {
                                    desc += s + ", ";
                                }
                                desc = desc.Remove(desc.Length - 2) + ")";
                                break;
                            case 3:
                                builder.WithColor(Color.Red);
                                if (ddBeingPlayed)
                                {
                                    desc += "\n" + players[playerInControl].Username + " incorrectly responded with \"" + response + "\" (Correct answer(s): ";
                                    foreach (string s in currentRound[categorySelected].Clues[valueSelected].Answers)
                                    {
                                        desc += s + ", ";
                                    }
                                    desc = desc.Remove(desc.Length - 2) + ")";
                                }
                                else
                                {
                                    desc += "\n" + players[playerThatBuzzedIn].Username + " incorrectly responded with \"" + response + "\"";
                                }
                                break;
                            case 4:
                                builder.WithColor(Color.LighterGrey);
                                desc += "\n" + players[playerThatBuzzedIn].User.Mention + " buzzed in! Type and send your response!";
                                break;
                            case 5:
                                builder.WithColor(Color.Red);
                                desc += "\n" + players[playerThatBuzzedIn].Username + " ran out of time!";
                                break;
                            case 6:
                                builder.WithColor(Color.Purple);
                                if (ddBeingPlayed)
                                {
                                    desc += "\n" + players[playerInControl].Username + " responded with \"" + response + "\"";
                                }
                                else
                                {
                                    desc += "\n" + players[playerThatBuzzedIn].Username + " responded with \"" + response + "\"";
                                }
                                break;
                            case 9:
                                builder.WithColor(Color.DarkPurple);
                                builder.WithFooter("Please be more specific");
                                break;
                        }
                    }
                    else
                    {
                        builder.WithColor(Color.Orange);
                        desc += "# " + players[playerInControl].Username + " found a DAILY DOUBLE!";
                        if (players[playerInControl].Score <= 1000 && round == 0)
                        {
                            builder.WithFooter("Type your wager. Min: 5/Max: 1000");
                            maxBet = 1000;
                        }
                        else if (players[playerInControl].Score <= 2000 && round == 1)
                        {
                            builder.WithFooter("Type your wager. Min: 5/Max: 2000");
                            maxBet = 2000;
                        }
                        else
                        {
                            builder.WithFooter("Type your wager. Min: 5/Max: " + players[playerInControl].Score);
                            maxBet = players[playerInControl].Score;
                        }
                    }
                }
                else
                {
                    builder.WithColor(Color.DarkMagenta);
                    builder.WithTitle(finalJ.Category);
                    desc = "# " + finalJ.Question;
                    builder.WithFooter("You can change your response as much as you like before time runs out.");
                }
                builder.WithDescription(desc);
                return builder;
            }

            async Task BuzzInTimer(bool dd = false)
            {
                while (timeToRespond > 0 && hasResponded == false)
                {
                    await Task.Delay(1000);
                    timeToRespond -= 1;
                }
                if (hasResponded == true)
                {

                }
                else
                {
                    if (dd == false)
                    {
                        players[playerThatBuzzedIn].Score -= currentRound[categorySelected].Clues[valueSelected].Value;
                        players[playerThatBuzzedIn].Coryat -= currentRound[categorySelected].Clues[valueSelected].Value;
                        players[playerThatBuzzedIn].Stats.IncorrectResponses += 1;
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(5).Build());
                        //ResetClueStuff(currentRound[categorySelected].Clues[valueSelected].Value);
                        canBuzz = true;
                        response = "";
                        timeToRespond = defaultTimeToRespond;
                        playerThatBuzzedIn = -1;
                        playersAttemptingToBuzz.Clear();

                        await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                        await Task.Delay(7000);
                        hasResponded = false;
                        canBuzz = false;
                        if (playersAttemptingToBuzz.Count == 0)
                        {
                            await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(1).Build()); //Triple stumper
                            await Task.Delay(3000);
                            if (CheckIfSwitch() == true)
                            {
                                await RoundEnded();
                            }
                            await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                        }
                        else
                        {
                            playerThatBuzzedIn = playersAttemptingToBuzz[rdm.Next(0, playersAttemptingToBuzz.Count)];
                            playersWhoHaveBuzzedIn.Add(playerThatBuzzedIn);
                            players[playerThatBuzzedIn].Stats.Buzzes += 1;
                            BuzzInTimer();
                            await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(4).Build());
                        }
                    }
                    else
                    {
                        players[playerInControl].Score -= ddWager;
                        players[playerInControl].Stats.IncorrectResponses += 1;
                        response = "[blank]";
                        dailyDoubleLog += players[playerInControl].Username + " lost " + ddWager + "\n";
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(3).Build());
                        ddBeingPlayed = false;
                        response = "";
                        playerThatBuzzedIn = -1;
                        playersAttemptingToBuzz.Clear();

                        await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                        await Task.Delay(4000);
                        hasResponded = false;
                        if (CheckIfSwitch() == true)
                        {
                            await RoundEnded();
                        }
                        await channelData.Clue.ModifyAsync(msg => msg.Components = ClueButtons(false).Build());
                        await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                    }
                }
            }

            async Task FinalJTimer()
            {



                while (timeToRespond > 0)
                {
                    await Task.Delay(1000);
                    timeToRespond -= 1;
                }

                await channelData.Clue.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
                finalJTimerUp = true;
                await channelData.Clue.ModifyAsync(m => m.Embed = FinalJEmbedEditor(2).Build());
                Player[] playersOrdered = players.OrderBy(player => player.Score).ToArray();
                await Task.Delay(4000);

                for (int i = 0; i < 3; i++)
                {
                    if (playersOrdered[i].Score <= 0) continue;
                    playerInControl = players.FindIndex(p => p.Username == playersOrdered[i].Username);
                    if (IsResponseAcceptable(finalJ.Answers, playersOrdered[i].FinalJeopardyResponse))
                    {
                        playersOrdered[i].Score += playersOrdered[i].FinalJeopardyWager;
                        await channelData.Clue.ModifyAsync(m => m.Embed = FinalJEmbedEditor(0).Build());
                    }
                    else if (verifyIncorrectResponses)
                    {
                        hasResponded = false;
                        wasAccepted = false;
                        await channelData.Clue.ModifyAsync(m => m.Embed = FinalJEmbedEditor(3).Build());
                        await channelData.Clue.ModifyAsync(m => m.Components = FinalJVerifyButtons().Build());
                        while (hasResponded == false)
                        {
                            await Task.Delay(1000);
                        }
                        await channelData.Clue.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
                        if (wasAccepted == true)
                        {
                            playersOrdered[i].Score += playersOrdered[i].FinalJeopardyWager;
                            await channelData.Clue.ModifyAsync(m => m.Embed = FinalJEmbedEditor(0).Build());
                        }
                        else
                        {
                            playersOrdered[i].Score -= playersOrdered[i].FinalJeopardyWager;
                            await channelData.Clue.ModifyAsync(m => m.Embed = FinalJEmbedEditor(1).Build());

                            await channelData.Board.ModifyAsync(m => m.Embed = BoardEmbed().Build());
                        }
                    }
                    else
                    {
                        playersOrdered[i].Score -= playersOrdered[i].FinalJeopardyWager;
                        await channelData.Clue.ModifyAsync(m => m.Embed = FinalJEmbedEditor(1).Build());
                    }
                    await channelData.Board.ModifyAsync(m => m.Embed = BoardEmbed().Build());
                    await Task.Delay(10000);
                }
                playersOrdered = players.OrderByDescending(player => player.Score).ToArray();
                EmbedBuilder finalResult = new EmbedBuilder().WithColor(Color.Gold).WithTitle("Results:");
                string text = "";
                for (int i = 0; i < 3; i++)
                {
                    if (playersOrdered[i].Score == playersOrdered[0].Score)
                    {
                        text += ":trophy: ";
                    }
                    else if (playersOrdered[i].Score == playersOrdered[1].Score)
                    {
                        text += ":second_place: ";
                    }
                    else
                    {
                        text += ":third_place: ";
                    }
                    text += playersOrdered[i].Username + " (" + playersOrdered[i].Score + ")\n";
                }
                finalResult.WithDescription(text);
                await channelData.Clue.ModifyAsync(m => m.Embed = finalResult.Build());
                await Task.Delay(5000);
                foreach (Player p in players)
                {
                    await channelData.Channel.SendMessageAsync(embed: BoxScore(p).Build());
                }
                await channelData.Channel.SendMessageAsync(dailyDoubleLog);
            }

            EmbedBuilder BoxScore(Player player) //0 = right, 1 = wrong, 2 = time up
            {
                EmbedBuilder builder = new EmbedBuilder();

                if (player.User != null)
                {
                    builder.WithThumbnailUrl(player.User.GetAvatarUrl() ?? player.User.GetDefaultAvatarUrl());
                }

                builder.WithTitle(player.Username + " Stats");

                string txt = "";

                txt += "Attempts: " + player.Stats.SingleJAttempts + "\n"
                    + "Buzzes: " + player.Stats.SingleJBuzzes + "\n"
                    + "Correct: " + player.Stats.SingleJCorrect + "\n"
                    + "Incorrect: " + player.Stats.SingleJIncorrect + "\n\n"
                    + "Score: " + player.Stats.SingleJScore + "\n"
                    ;

                builder.AddField("Jeopardy Round Stats", txt);

                txt = "";

                txt += "Attempts: " + player.Stats.DoubleJAttempts + "\n"
                    + "Buzzes: " + player.Stats.DoubleJBuzzes + "\n"
                    + "Correct: " + player.Stats.DoubleJCorrect + "\n"
                    + "Incorrect: " + player.Stats.DoubleJIncorrect + "\n\n"
                    + "Score: " + player.Stats.DoubleJScore + "\n"
                    ;

                builder.AddField("Double Jeopardy Stats", txt);

                txt = "Final Score: " + player.Score;
                builder.AddField("Final Jeopardy", txt);

                txt = "Attempts: " + player.Stats.Attempts + "\n"
                    + "Buzzes: " + player.Stats.Buzzes + "\n"
                    + "Correct: " + player.Stats.CorrectResponses + "\n"
                    + "Incorrect: " + player.Stats.IncorrectResponses + "\n\n"
                    + "Coryat: " + player.Coryat + "\n"
                    ;
                builder.AddField("Totals", txt);

                return builder;
            }

            EmbedBuilder FinalJEmbedEditor(int type = 0) //0 = right, 1 = wrong, 2 = time up
            {
                EmbedBuilder builder = new EmbedBuilder();
                string desc = "";
                builder.WithTitle(finalJ.Category);
                desc = "# " + finalJ.Question;
                string finalJResponse = players[playerInControl].FinalJeopardyResponse;
                switch (type)
                {
                    case 0:
                        builder.WithColor(Color.Green);
                        desc += "\n" + players[playerInControl].Username + " correctly answered with \"" + finalJResponse + "\" (Correct" +
                            " answer(s): ";
                        foreach (string s in finalJ.Answers)
                        {
                            desc += s + ", ";
                        }
                        desc = desc.Remove(desc.Length - 2) + ")";
                        desc += "\n\n**(+" + players[playerInControl].FinalJeopardyWager + ")**";
                        break;
                    case 1:
                        builder.WithColor(Color.Red);
                        desc += "\n" + players[playerInControl].Username + " incorrectly responded with \"" + finalJResponse + "\"";
                        desc += "\n\n**(-" + players[playerInControl].FinalJeopardyWager + ")**";
                        break;
                    case 2:
                        builder.WithColor(Color.DarkGrey);
                        builder.WithFooter("Time's up!");
                        break;
                    case 3:
                        builder.WithColor(Color.Purple);
                        desc += "\n" + players[playerInControl].Username + " responded with \"" + finalJResponse + "\"";
                        break;
                }


                builder.WithDescription(desc);
                return builder;
            }

            ComponentBuilder FinalJVerifyButtons()
            {
                ComponentBuilder builder = new ComponentBuilder();
                builder.WithButton("Accept", "accept", ButtonStyle.Success);
                builder.WithButton("Deny", "deny", ButtonStyle.Danger);
                return builder;
            }

            async Task JoinGame(SocketInteractionContext ctx)
            {
                players.Add(new Player(ctx.User, ctx.User.Username, false, 0));
                await channelData.Board.ModifyAsync(msg => msg.Embed = JoinScreen().Build());
            }

            async Task LeaveGame(SocketInteractionContext ctx)
            {
                players.RemoveAll(player => player.Username == ctx.User.Username);
                await channelData.Board.ModifyAsync(msg => msg.Embed = JoinScreen().Build());
            }

            async Task JoinGameHost(SocketInteractionContext ctx)
            {
                hosts.Add(ctx.User);
                await channelData.Board.ModifyAsync(msg => msg.Embed = JoinScreen().Build());
            }

            async Task LeaveGameHost(SocketInteractionContext ctx)
            {
                hosts.Remove(ctx.User);
                await channelData.Board.ModifyAsync(msg => msg.Embed = JoinScreen().Build());
            }

            public static bool IsCloseEnough(string cleanedPlayerResponse, string[] cleanedCorrectResponses)
            {
                if (cleanedPlayerResponse.Length <= needsToBeExactLength)
                {
                    return false;
                }
                // Define a maximum Damerau-Levenshtein distance for acceptance
                int maxDistance = lenienceExtent;

                foreach (string correctResponse in cleanedCorrectResponses)
                {
                    int distance = ComputeDamerauLevenshteinDistance(cleanedPlayerResponse, correctResponse);
                    if (distance <= maxDistance)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static int ComputeDamerauLevenshteinDistance(string str1, string str2) //Idk what this really does but ChatGPT made it and it worked lol
            {
                int[,] dp = new int[str1.Length + 1, str2.Length + 1];

                for (int i = 0; i <= str1.Length; i++)
                {
                    for (int j = 0; j <= str2.Length; j++)
                    {
                        if (i == 0)
                        {
                            dp[i, j] = j;
                        }
                        else if (j == 0)
                        {
                            dp[i, j] = i;
                        }
                        else
                        {
                            int cost = (str1[i - 1] == str2[j - 1]) ? 0 : 1;
                            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);

                            if (i > 1 && j > 1 && str1[i - 1] == str2[j - 2] && str1[i - 2] == str2[j - 1])
                            {
                                dp[i, j] = Math.Min(dp[i, j], dp[i - 2, j - 2] + cost);
                            }
                        }
                    }
                }

                return dp[str1.Length, str2.Length];
            }

            public static bool IsResponseAcceptable(string[] correctResponses, string playerResponse)
            {
                List<string> newCorrectResponses = new List<string>();

                List<string> exactAnswers = new List<string>();
                foreach (string s in correctResponses)
                {
                    if (s.StartsWith("*"))
                    {
                        if (s.ToLower().Remove(0, 1) == playerResponse.ToLower())
                        {
                            return true;
                        }
                    }
                    else
                    {
                        newCorrectResponses.Add(s);
                    }
                }



                // Replace "&" with "and" and remove non-alphanumeric characters and spaces
                string cleanedPlayerResponse = new string(playerResponse
                    .Replace("&", "and")
                    .Where(char.IsLetterOrDigit)
                    .ToArray());

                cleanedPlayerResponse = cleanedPlayerResponse.ToLower();

                // Clean and convert all correct responses to lower case
                string[] cleanedCorrectResponses = correctResponses
                    .Select(response => new string(response
                        .Replace("&", "and")
                        .Where(char.IsLetterOrDigit)
                        .ToArray())
                        .ToLower())
                    .ToArray();

                // Step 2: Check if the cleaned player response matches any correct response
                if (cleanedCorrectResponses.Any(cr => cleanedPlayerResponse.Equals(cr)))
                {
                    return true;
                }

                // Step 3: Check if the player response without "the", "a", or "an" matches any correct response
                string[] wordsToRemove = { "the", "a", "an", "whatis", "whats", "wheres", "whereis", "whyis", "whys",
            "howis", "hows", "whos", "whois", "s",
            "whatisa", "whatisthe", "whatisan", "whereisa", "whereisthe", "whereisan", "whyisa", "whyisthe", "whyisan",
            "whoisa", "whoisthe", "whoisan", "howisa", "howisthe", "howisan",
            "whatsa", "whatsthe", "whatsan", "wheresa", "wheresthe", "wheresan", "whysa", "whysthe", "whysan",
            "whosa", "whosthe", "whosan", "howsa", "howsthe", "howsan", "whatare", "whoare", "whereare"};
                foreach (var wordToRemove in wordsToRemove)
                {
                    if (cleanedPlayerResponse.StartsWith(wordToRemove) &&
                        cleanedCorrectResponses.Contains(cleanedPlayerResponse.Remove(0, wordToRemove.Length)))
                    {
                        return true;
                    }
                    if (wordToRemove == "s")
                    {
                        if (cleanedPlayerResponse.EndsWith(wordToRemove) &&
                        cleanedCorrectResponses.Contains(cleanedPlayerResponse.Remove(cleanedPlayerResponse.Length - 1, 1)))
                        {
                            return true;
                        }
                    }
                }

                if (lenient == true)
                {
                    // Step 4: Check if the player's response is at least 80% correct
                    if (IsCloseEnough(cleanedPlayerResponse, cleanedCorrectResponses))
                    {
                        return true;
                    }

                    // Step 5: Check if the player response without "the", "a", or "an" is close enough to any correct response
                    foreach (var wordToRemove in wordsToRemove)
                    {
                        if (cleanedPlayerResponse.StartsWith(wordToRemove) &&
                            IsCloseEnough(cleanedPlayerResponse.Remove(0, wordToRemove.Length), cleanedCorrectResponses))
                        {
                            return true;
                        }
                        if (wordToRemove == "s")
                        {
                            if (cleanedPlayerResponse.EndsWith(wordToRemove) &&
                            IsCloseEnough(cleanedPlayerResponse.Remove(cleanedPlayerResponse.Length - 1, 1), cleanedCorrectResponses))
                            {
                                return true;
                            }
                        }
                    }
                }
                // Step 6: If all checks fail, return false
                return false;
            }

            async Task BuzzAttempt(SocketMessageComponent component, SocketInteractionContext ctx)
            {
                int indexNo = players.FindIndex(p => p.Username == ctx.User.Username);
                if (players.Any(p => p.Username == ctx.User.Username) && canBuzz == true && !playersAttemptingToBuzz.Contains(indexNo)
                    && !playersWhoHaveBuzzedIn.Contains(indexNo))
                {
                    playersAttemptingToBuzz.Add(indexNo);
                    players[indexNo].Stats.Attempts += 1;
                    await component.RespondAsync("You are attempting to buzz in!", ephemeral: true);
                }
                else if (players.Any(p => p.Username == ctx.User.Username) && canBuzz == true && playersAttemptingToBuzz.Contains(indexNo)
                    && !playersWhoHaveBuzzedIn.Contains(indexNo))
                {
                    playersAttemptingToBuzz.Remove(indexNo);
                    players[indexNo].Stats.Attempts -= 1;
                    await component.RespondAsync("You are no longer attempting to buzz in", ephemeral: true);
                }
                else
                {
                    await component.RespondAsync("You can't", ephemeral: true);
                }
            }

            async Task CategorySelect(SocketMessageComponent component, SocketInteractionContext ctx, int category)
            {
                if ((round == 0 || round == 1) && ctx.User.Username == players[playerInControl].Username)
                {
                    categorySelected = category;
                    await component.DeferAsync();
                    await channelData.Board.ModifyAsync(msg => msg.Components = BoardMoneyComponents().Build());
                    await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                }
                else
                {
                    await component.RespondAsync("Wait for your turn!", ephemeral: true);
                }
            }

            void ResetClueStuff(int value)
            {
                hasResponded = false;
                response = "";
                timeToRespond = defaultTimeToRespond;
                playerThatBuzzedIn = -1;
                playersAttemptingToBuzz.Clear();
            }

            async Task DisplayDailyDoubleQuestion()
            {
                hasResponded = false;
                await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue().Build());
                if (currentRound[categorySelected].Clues[valueSelected].ImageLink != "N/A")
                {
                    await channelData.Image.ModifyAsync(msg => msg.Content = currentRound[categorySelected].Clues[valueSelected].ImageLink);
                }
                else
                {
                    await channelData.Image.ModifyAsync(msg => msg.Content = "`----------------`");
                }
                Console.WriteLine(timeToRespond = defaultTimeToRespondDD * 1000 +
                    (currentRound[categorySelected].Clues[valueSelected].Question.Split(" ").Length / wordsForAnotherSecond) *
                    1000);
                timeToRespond = defaultTimeToRespondDD +
                    (currentRound[categorySelected].Clues[valueSelected].Question.Split(" ").Length / wordsForAnotherSecond);
                BuzzInTimer(true);
            }

            async Task ValueSelect(SocketMessageComponent component, SocketInteractionContext ctx, int value)
            {
                if ((round == 0 || round == 1) && ctx.User.Username == players[playerInControl].Username)
                {
                    valueSelected = value;
                    currentRound[categorySelected].Clues[value].Played = true;
                    ResetClueStuff(value);
                    playersWhoHaveBuzzedIn.Clear();
                    await component.DeferAsync();
                    if (currentRound[categorySelected].Clues[value].IsDD == false)
                    {
                        canBuzz = true;
                        await channelData.Board.ModifyAsync(msg => msg.Components = new ComponentBuilder().Build());
                        await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue().Build());
                        if (currentRound[categorySelected].Clues[value].ImageLink != "N/A")
                        {
                            await channelData.Image.ModifyAsync(msg => msg.Content = currentRound[categorySelected].Clues[value].ImageLink);
                        }
                        else
                        {
                            await channelData.Image.ModifyAsync(msg => msg.Content = "`----------------`");
                        }
                        await Task.Delay(defaultDelay +
                            (currentRound[categorySelected].Clues[valueSelected].Question.Split(" ").Length / wordsForAnotherSecond) * 1000);

                        if (playersAttemptingToBuzz.Count == 0)
                        {
                            await Task.Delay(4000);
                            canBuzz = false;
                            if (playersAttemptingToBuzz.Count == 0)
                            {
                                await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(1).Build());
                                await Task.Delay(3000);
                                await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                            }
                            else
                            {
                                BuzzedIn();
                            }
                        }
                        else
                        {
                            canBuzz = false;
                            BuzzedIn();
                        }
                    }
                    else
                    {
                        ddBeingPlayed = true;
                        ddWager = 0;
                        await channelData.Board.ModifyAsync(msg => msg.Components = new ComponentBuilder().Build());
                        await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(7).Build());
                        await channelData.Clue.ModifyAsync(msg => msg.Components = new ComponentBuilder().Build());
                        waitingForWager = true;
                    }
                }
                else
                {
                    await component.RespondAsync("Wait for your turn!");
                }
            }

            async Task BuzzedIn(bool bms = false)
            {
                if (bms == false)
                {
                    playerThatBuzzedIn = playersAttemptingToBuzz[rdm.Next(0, playersAttemptingToBuzz.Count)];
                    playersWhoHaveBuzzedIn.Add(playerThatBuzzedIn);
                }
                if (bms == false)
                {
                    players[playerThatBuzzedIn].Stats.Buzzes += 1;
                    BuzzInTimer();
                    await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(4).Build());
                }
                else
                {
                    BuzzInTimer();
                    await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(9).Build());
                }

            }

            async Task Answer(SocketCommandContext ctx)
            {
                response = ctx.Message.Content;
                hasResponded = true;
                if (IsResponseAcceptable(currentRound[categorySelected].Clues[valueSelected].Answers, response))
                {
                    Right();
                }
                else
                {
                    if (verifyIncorrectResponses)
                    {
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(6).Build());
                        await channelData.Clue.ModifyAsync(msg => msg.Components = ClueButtons(true).Build());
                    }
                    else
                    {
                        Wrong();
                    }
                }
            }

            async Task Right()
            {

                if (ddBeingPlayed == false)
                {
                    players[playerThatBuzzedIn].Stats.CorrectResponses += 1;
                    playerInControl = playerThatBuzzedIn;
                    players[playerThatBuzzedIn].Score += currentRound[categorySelected].Clues[valueSelected].Value;
                    players[playerThatBuzzedIn].Coryat += currentRound[categorySelected].Clues[valueSelected].Value;
                    await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(2).Build());
                    await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                    await Task.Delay(3000);
                    if (CheckIfSwitch() == true)
                    {
                        await RoundEnded();
                    }
                    await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                }
                else
                {
                    players[playerInControl].Stats.CorrectResponses += 1;
                    players[playerInControl].Score += ddWager;
                    players[playerInControl].Coryat += currentRound[categorySelected].Clues[valueSelected].Value;
                    await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(2).Build());
                    await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                    await Task.Delay(3000);
                    ddBeingPlayed = false;

                    dailyDoubleLog += players[playerInControl].Username + " earned " + ddWager + "\n";

                    if (CheckIfSwitch() == true)
                    {
                        await RoundEnded();
                    }
                    await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                    await channelData.Clue.ModifyAsync(msg => msg.Components = ClueButtons(false).Build());
                }
            }

            async Task Wrong()
            {
                if (ddBeingPlayed == false)
                {
                    players[playerThatBuzzedIn].Stats.IncorrectResponses += 1;
                    players[playerThatBuzzedIn].Score -= currentRound[categorySelected].Clues[valueSelected].Value;
                    players[playerThatBuzzedIn].Coryat -= currentRound[categorySelected].Clues[valueSelected].Value;
                    await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(3).Build());

                    //ResetClueStuff(currentRound[categorySelected].Clues[valueSelected].Value);
                    canBuzz = true;
                    response = "";
                    timeToRespond = defaultTimeToRespond;
                    playerThatBuzzedIn = -1;
                    playersAttemptingToBuzz.Clear();

                    await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                    await Task.Delay(7000);
                    hasResponded = false;

                    if (playersAttemptingToBuzz.Count == 0)
                    {
                        canBuzz = false;
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(1).Build()); //Triple stumper
                        await Task.Delay(3000);
                        if (CheckIfSwitch() == true)
                        {
                            await RoundEnded();
                        }
                        await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                    }
                    else
                    {
                        canBuzz = false;
                        playerThatBuzzedIn = playersAttemptingToBuzz[rdm.Next(0, playersAttemptingToBuzz.Count)];
                        playersWhoHaveBuzzedIn.Add(playerThatBuzzedIn);
                        players[playerThatBuzzedIn].Stats.Buzzes += 1;
                        BuzzInTimer();
                        await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(4).Build());
                    }
                }
                else
                {
                    players[playerInControl].Stats.IncorrectResponses += 1;
                    players[playerInControl].Score -= ddWager;
                    await channelData.Clue.ModifyAsync(msg => msg.Embed = DisplayClue(3).Build());
                    await channelData.Board.ModifyAsync(msg => msg.Embed = BoardEmbed().Build());
                    await Task.Delay(3000);
                    ddBeingPlayed = false;
                    if (CheckIfSwitch() == true)
                    {
                        await RoundEnded();
                    }
                    dailyDoubleLog += players[playerInControl].Username + " lost " + ddWager + "\n";
                    await channelData.Board.ModifyAsync(msg => msg.Components = BoardCategoryComponents().Build());
                    await channelData.Clue.ModifyAsync(msg => msg.Components = ClueButtons(false).Build());
                }
            }

            public async Task ButtonClicked(SocketMessageComponent component, SocketInteractionContext ctx)
            {

                bool playerIn = players.Any(player => player.Username == ctx.User.Username);
                switch (component.Data.CustomId)
                {
                    case "join":
                        if (playerIn)
                        {
                            //await component.RespondAsync("You're already playing!", ephemeral: true);
                        }
                        else if (players.Count < 3)
                        {
                            //players.Add(new Player(null, "Ted", 0, 0, 0, true, 0));
                            //players.Add(new Player(null, "Benjamin", true, 0));
                            await JoinGame(ctx);
                            await component.RespondAsync("You have joined the game!", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("The game is full!", ephemeral: true);
                        }
                        break;
                    case "leave":
                        if (playerIn)
                        {
                            await LeaveGame(ctx);
                            await component.RespondAsync("You have left the game", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("You cannot leave a game that you're not in", ephemeral: true);
                        }
                        break;
                    case "host":
                        if (!hosts.Contains(ctx.User))
                        {
                            await JoinGameHost(ctx);
                            await component.RespondAsync("You are now a host!", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("You are already a host!", ephemeral: true);
                        }
                        break;
                    case "hostleave":
                        if (hosts.Contains(ctx.User))
                        {
                            await LeaveGameHost(ctx);
                            await component.RespondAsync("You are no longer a host!", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("You aren't even a host!", ephemeral: true);
                        }
                        break;
                    case "start":
                        if (players.Count == 3)
                        {
                            await StartGame(ctx);
                            await component.DeferAsync();
                        }
                        else
                        {
                            await component.RespondAsync("You need 3 players to start! Try adding bots?");
                        }
                        break;
                    case "accept":
                        if (hosts.Any(h => h.Username == ctx.User.Username))
                        {
                            if (round == 2)
                            {
                                hasResponded = true;
                                wasAccepted = true;
                            }
                            else
                            {
                                await channelData.Clue.ModifyAsync(m => m.Components = ClueButtons(false).Build());
                                Right();
                            }
                            await component.DeferAsync();
                        }
                        else
                        {
                            await component.RespondAsync("You aren't a host!", ephemeral: true);
                        }
                        break;
                    case "deny":
                        if (hosts.Any(h => h.Username == ctx.User.Username))
                        {
                            if (round == 2)
                            {
                                hasResponded = true;
                                wasAccepted = false;
                            }
                            else
                            {
                                await channelData.Clue.ModifyAsync(m => m.Components = ClueButtons(false).Build());
                                Wrong();
                            }
                            await component.DeferAsync();
                        }
                        else
                        {
                            await component.RespondAsync("You aren't a host!", ephemeral: true);
                        }
                        break;
                    case "bms":
                        if (hosts.Any(h => h.Username == ctx.User.Username))
                        {
                            if (!ddBeingPlayed)
                            {
                                await channelData.Clue.ModifyAsync(m => m.Components = ClueButtons(false).Build());
                                hasResponded = false;
                                response = "";
                                timeToRespond = defaultTimeToRespond;
                                BuzzedIn(true);
                            }
                            else
                            {
                                await channelData.Clue.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
                                hasResponded = false;
                                response = "";
                                timeToRespond = defaultTimeToRespond;
                            }
                            await component.DeferAsync();
                        }
                        else
                        {
                            await component.RespondAsync("You aren't a host!", ephemeral: true);
                        }
                        break;
                    case "Category0":
                        await CategorySelect(component, ctx, 0);
                        break;
                    case "Category1":
                        await CategorySelect(component, ctx, 1);
                        break;
                    case "Category2":
                        await CategorySelect(component, ctx, 2);
                        break;
                    case "Category3":
                        await CategorySelect(component, ctx, 3);
                        break;
                    case "Category4":
                        await CategorySelect(component, ctx, 4);
                        break;
                    case "Category5":
                        await CategorySelect(component, ctx, 5);
                        break;
                    case "Value0":
                        ValueSelect(component, ctx, 0);
                        break;
                    case "Value1":
                        ValueSelect(component, ctx, 1);
                        break;
                    case "Value2":
                        ValueSelect(component, ctx, 2);
                        break;
                    case "Value3":
                        ValueSelect(component, ctx, 3);
                        break;
                    case "Value4":
                        ValueSelect(component, ctx, 4);
                        break;
                    case "buzz":
                        await BuzzAttempt(component, ctx);
                        break;
                    case "endround":
                        if (hosts.Contains(ctx.User))
                        {
                            SwitchRound();
                            await RoundEnded();
                            await component.RespondAsync("Round ended", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("You are not a host!", ephemeral: true);
                        }
                        break;
                    case "wager":
                        if (round == 2 && players.Any(p => p.Username == ctx.User.Username))
                        {
                            await component.RespondWithModalAsync(WagerModal().Build());
                        }
                        else
                        {
                            await component.RespondAsync("You can't");
                        }
                        break;
                    case "answer":
                        if (round == 2 && players.Any(p => p.Username == ctx.User.Username))
                        {
                            await component.RespondWithModalAsync(FinalJModal().Build());
                        }
                        else
                        {
                            await component.RespondAsync("You can't");
                        }
                        break;
                    case "correctscoring":
                        if (hosts.Any(h => h.Username == ctx.User.Username))
                        {
                            await channelData.Board.ModifyAsync(m => m.Components = ScoringCorrectionComponents().Build());
                            await component.DeferAsync();
                        }
                        else
                        {
                            await component.RespondAsync("You aren't a host!", ephemeral: true);
                        }
                        break;
                    case "correctscoringfr":
                        if (hosts.Any(h => h.Username == ctx.User.Username))
                        {
                            await component.RespondWithModalAsync(ScoreCorrectModal().Build());
                        }
                        else
                        {
                            await component.RespondAsync("You aren't a host!", ephemeral: true);
                        }
                        break;
                    case "back":
                        if (hosts.Any(h => h.Username == ctx.User.Username))
                        {
                            await channelData.Board.ModifyAsync(m => m.Components = BoardCategoryComponents().Build());
                            await component.DeferAsync();
                        }
                        else
                        {
                            await component.RespondAsync("You aren't a host!", ephemeral: true);
                        }
                        break;
                }
            }

            public async Task HandleCommand(SocketCommandContext context)
            {
                int argPos = 0;
                string messageContext = context.Message.Content.ToLower();
                if (!context.User.IsBot)
                {
                    if (channelData != null)
                    {
                        if (context.User == players[playerInControl].User && ddWager == 0 && waitingForWager && ddBeingPlayed)
                        {
                            int wage = 0;
                            if (int.TryParse(context.Message.Content, out wage) == true)
                            {
                                if (wage >= 5 && wage <= maxBet)
                                {
                                    ddWager = wage;
                                    waitingForWager = false;
                                    DisplayDailyDoubleQuestion();
                                }
                            }
                        }
                        else if (context.User == players[playerInControl].User && ddWager >= 5 && ddBeingPlayed)
                        {
                            Answer(context);
                        }
                        else if (playerThatBuzzedIn != -1)
                        {
                            if (context.User.Username == players[playerThatBuzzedIn].Username && canBuzz == false &&
                                context.Channel == channelData.Channel && response == "")
                            {
                                Answer(context);
                            }
                        }
                        if (context.Channel == channelData.Channel)
                        {
                            await context.Message.DeleteAsync();
                        }
                    }
                }
            }

            public async Task HandleModal(SocketModal modal, List<SocketMessageComponentData> components)
            {
                foreach (SocketMessageComponentData component in components)
                {
                    if (component.CustomId == "wager")
                    {
                        string fjWager = components
                        .First(x => x.CustomId == "wager").Value;

                        int playerNo = players.FindIndex(p => p.Username == modal.User.Username);
                        int wagerAmount = 0;

                        if (int.TryParse(fjWager, out wagerAmount) && wagerAmount >= 0 && wagerAmount <= players[playerNo].Score)
                        {
                            if (playerNo > -1)
                            {
                                players[playerNo].MadeFinalWager = true;
                                players[playerNo].FinalJeopardyWager = wagerAmount;
                                await modal.RespondAsync("You are wagering " + fjWager, ephemeral: true);
                                if (players.All(p => p.MadeFinalWager) && !finalJDidStart)
                                {
                                    finalJDidStart = true;
                                    string mention = "FINAL JEOPARDY IS STARTING: ";
                                    foreach (var player in players)
                                    {
                                        if (player.User != null)
                                        {
                                            mention += player.User.Mention + " ";
                                        }
                                    }
                                    await channelData.Channel.SendMessageAsync(mention);
                                    await Task.Delay(10000);

                                    await channelData.Board.ModifyAsync(b => b.Components = new ComponentBuilder().Build());
                                    await channelData.Board.ModifyAsync(b => b.Embed = BoardEmbed().Build());
                                    await channelData.Clue.ModifyAsync(b => b.Embed = DisplayClue(8).Build());
                                    if (finalJ.ImageLink == "N/A")
                                    {
                                        await channelData.Image.ModifyAsync(m => m.Content = "`----------------`");
                                    }
                                    else
                                    {
                                        await channelData.Image.ModifyAsync(m => m.Content = finalJ.ImageLink);
                                    }
                                    timeToRespond = defaultTimeToRespondFJ +
                                    finalJ.Question.Split(" ").Length
                                    / wordsForAnotherSecond;
                                    await channelData.Clue.ModifyAsync(b => b.Components = new ComponentBuilder()
                                    .WithButton("ANSWER", "answer", ButtonStyle.Success).Build());

                                    FinalJTimer();
                                }
                            }
                            else
                            {
                                await modal.RespondAsync("You're not playing!", ephemeral: true);
                            }
                        }
                        else
                        {
                            await modal.RespondAsync("Your wager is invalid!", ephemeral: true);
                        }
                    }
                    else if (component.CustomId == "response")
                    {
                        string fjResponse = components
                        .First(x => x.CustomId == "response").Value;

                        if (!finalJTimerUp)
                        {
                            int playerNo = players.FindIndex(p => p.Username == modal.User.Username);
                            if (playerNo > -1)
                            {
                                players[playerNo].FinalJeopardyResponse = fjResponse;
                                await modal.RespondAsync("You answered: " + fjResponse, ephemeral: true);
                            }
                            else
                            {
                                await modal.RespondAsync("You're not playing!", ephemeral: true);
                            }
                        }
                        else
                        {
                            await modal.RespondAsync("Time is up.", ephemeral: true);
                        }
                    }
                    else if (component.CustomId == "playerno")
                    {
                        int.TryParse(components.First(x => x.CustomId == "playerno").Value, out int pNo);
                        int.TryParse(components.First(x => x.CustomId == "score").Value, out int score);
                        int.TryParse(components.First(x => x.CustomId == "coryat").Value, out int coryat);
                        int.TryParse(components.First(x => x.CustomId == "correct").Value, out int correct);
                        int.TryParse(components.First(x => x.CustomId == "incorrect").Value, out int incorrect);
                        pNo -= 1;
                        if (pNo > -1 && pNo < 3)
                        {
                            players[pNo].Score += score;
                            players[pNo].Coryat += coryat;
                            players[pNo].Stats.CorrectResponses += correct;
                            players[pNo].Stats.IncorrectResponses += incorrect;
                            await channelData.Board.ModifyAsync(m => m.Embed = BoardEmbed().Build());
                            await modal.RespondAsync("Done!", ephemeral: true);
                        }
                        else
                        {
                            await modal.RespondAsync("Please input player number between 1-3", ephemeral: true);
                        }
                    }

                }
            }

            #endregion
        }

        public async Task RunBotAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All,
                UseInteractionSnowflakeDate = false
            });
            _commands = new CommandService();
            _client.Log += _client_Log;
            _client.Ready += () =>
            {
                return Task.CompletedTask;

            };
            await RegisterCommandsAsync();
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task _client_Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            _client.ButtonExecuted += MyButtonHandler;
            _client.ModalSubmitted += async modal =>
            {
                await HandleModalAsync(modal);
            };
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        // Define a method to handle modal events
        private async Task HandleModalAsync(SocketModal modal)
        {
            List<SocketMessageComponentData> components =
                modal.Data.Components.ToList();

            int i = -1;

            // Iterate over the games list
            for (int index = 0; index < games.Count; index++)
            {
                // Check if the channelData of the current game matches
                if (games[index].channelData.Channel.Id == modal.Channel.Id)
                {
                    i = index; // Assign the index of the matching game to i
                    break; // Exit the loop once a match is found
                }
            }

            if (i > -1)
            {
                try
                {
                    await games[i].HandleModal(modal, components);
                }
                catch
                {

                }
            }
        }

        public string Shuffle(string str)
        {
            Random rand = new Random();
            var list = new SortedList<int, char>();
            foreach (var c in str)
                list.Add(rand.Next(), c);
            return new string(list.Values.ToArray());
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (_client != null && message != null)
            {
                var context = new SocketCommandContext(_client, message);
                int i = -1;

                // Iterate over the games list
                for (int index = 0; index < games.Count; index++)
                {
                    // Check if the channelData of the current game matches
                    if (games[index].channelData.Channel.Id == context.Channel.Id)
                    {
                        i = index; // Assign the index of the matching game to i
                        break; // Exit the loop once a match is found
                    }
                }

                if (i > -1)
                {
                    try
                    {
                        await games[i].HandleCommand(context);
                    } 
                    catch
                    {

                    }
                } else
                {
                    if (!context.User.IsBot)
                    {
                        if (context.Message.Content == prefix + "initialize")
                        {
                            JeopardyGame newGame = new JeopardyGame();
                            await newGame.InitializeGame(context);
                            games.Add(newGame);
                            //players.Add(new Player(null, "Ted", true, 0));
                            await context.Message.DeleteAsync();
                        }
                    }
                }
            }
        }

        public async Task MyButtonHandler(SocketMessageComponent component)
        {
            SocketInteractionContext ctx = new SocketInteractionContext(_client, component);
            int i = -1;

            // Iterate over the games list
            for (int index = 0; index < games.Count; index++)
            {
                // Check if the channelData of the current game matches
                if (games[index].channelData.Channel.Id == ctx.Channel.Id)
                {
                    i = index; // Assign the index of the matching game to i
                    break; // Exit the loop once a match is found
                }
            }

            if (i > -1)
            {
                try
                {
                    await games[i].ButtonClicked(component, ctx);
                }
                catch
                {

                }
            }
        }

    }
}