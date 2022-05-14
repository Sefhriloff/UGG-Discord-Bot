
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LolBot
{
    class Program
    {
        string Path = AppDomain.CurrentDomain.BaseDirectory;

        public Configuration Configs;
        public Dictionary<string, Champion> Champions;
        public Dictionary<string, int[]> DefaultRoles;
        JArray RUNAS_JSON;

        public Dictionary<string, int> LRunesID = new Dictionary<string, int>()
        {
            {"8100", 0},
            {"8300", 1},
            {"8000", 2},
            {"8400", 3},
            {"8200", 4}
        };

        static void Main(string[] args)
        {
            new Program().RunBotAsync().GetAwaiter().GetResult();
        }
        public string getChampionID(string name)
        {
            return Champions.FirstOrDefault(x => x.Value.name.ToLower().Contains(name.ToLower())).Key;
        }

        public string getChampionName(string id)
        {
            return Champions.GetValueOrDefault(id).name;
        }

        public int[] getRoles(string id)
        {
            return DefaultRoles.GetValueOrDefault(id);
        }

        public string getRoleName(string id)
        {
            switch (id)
            {
                case "1": return Configs.role_jungle;
                case "2": return Configs.role_sup;
                case "3": return Configs.role_adc;
                case "4": return Configs.role_top;
                case "5": return Configs.role_mid;
                default: return Configs.role_none;
            }
        }
        public void log(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void logError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public bool loadConfiguration()
        {
            if (File.Exists(Path + "config.ini"))
            {
                Configs = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(Path + "config.ini"));
                log("Sucessfully loaded bot configuration.");
                return true;
            }
            logError("config.ini not found :'( *crying*");
            return false;
        }
        public void loadChampions()
        {
            string json = downloadJson(Configs.champions_names_url);
            Champions = JsonConvert.DeserializeObject<Dictionary<string, Champion>>(json);
            log("Loaded Champions.");
        }

        public void loadRoles()
        {
            string json = downloadJson(Configs.champions_roles_url.Replace("{patch}", Configs.patch));
            DefaultRoles = JsonConvert.DeserializeObject<Dictionary<string, int[]>>(json);
            log("Loaded Default Roles.");
        }

        public void loadRunes()
        {
            if (File.Exists(Path + "Runas.json"))
            {
                RUNAS_JSON = JArray.Parse(File.ReadAllText(Path + "Runas.json"));
                log("Loaded Runes.");
            }
            else { logError("Missing Runas.json you can download from (https://static.u.gg/assets/lol/riot_static/12.9.1/data/pt_BR/runesReforged.json)");}
        }

        public string downloadJson(string url)
        {
            using (WebClient client = new WebClient())
            {
                try
                {
                    return client.DownloadString(url);
                }
                catch (Exception ex)
                {
                    logError("Error downloading json from: " + url);
                    return null;
                }
            }
        }

        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        public async Task RunBotAsync()
        {
            log("Initiating.");

            if (!loadConfiguration()) { return; }

            loadChampions();
            loadRoles();
            loadRunes();


            _client = new DiscordSocketClient();
            _commands = new CommandService();

            _services = new ServiceCollection().AddSingleton(_client).AddSingleton(_commands).BuildServiceProvider();

            _client.Log += _client_Log;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, Configs.token);

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
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            if (message == null) { return; }
            if (message.Author.IsBot) return;

            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos))
            {
                string[] content = message.Content.ToLower().Split(" ");

                switch (content[0])
                {
                    case "!ajuda":
                        await context.Channel.SendMessageAsync("```Comandos:\r\n!runas <campeão> <função>\r\n\r\nFunções Disponiveis:\r\nadc,mid,sup,top,jg```");
                        break;

                    case "!runas":
                        if (content.Length < 2) { return; }
                        string ChampID = getChampionID(content[1]);
                        if (ChampID != null)
                        {
                            int[] defaultRoles = getRoles(ChampID);
                            string Role = "6";
                            if (defaultRoles != null) { Role = defaultRoles[0].ToString(); }
                            if (content.Length > 2)
                            {
                                // Expecting role name here
                                Role = getRoleByName(content[2]);
                                if (Role == null) { Role = defaultRoles[0].ToString(); }
                            }
                            await context.Channel.SendMessageAsync(Configs.get_runes_text.Replace("{name}", getChampionName(ChampID)).Replace("{role}", getRoleName(Role)));

                            if (!getChampOverview(ChampID, Role))
                            {
                                logError("Error loading champion runes");
                                return;
                            }

                            await context.Channel.SendFileAsync(Path + "test.png", Configs.here_your_runes_text);
                        }
                        else { await context.Channel.SendMessageAsync(Configs.champ_not_found_text); }
                        break;
                    default:
                        // await context.Channel.SendMessageAsync("Unknown command! " + header); 
                        break;
                }
            }
        }

        public string getRoleByName(string input)
        {
            if (Configs.jg_common_words.Contains(input)) { return "1"; }
            else if (Configs.top_common_words.Contains(input)) { return "4"; }
            else if (Configs.adc_common_words.Contains(input)) { return "3"; }
            else if (Configs.sup_common_words.Contains(input)) { return "2"; }
            else if (Configs.mid_common_words.Contains(input)) { return "5"; }
            return null;
        }

        private bool getChampOverview(string id, string role)
        {
            try
            {
                string json = downloadJson(Configs.champion_runes_url.Replace("{patch}", Configs.patch).Replace("{id}", id));

                JObject ChampData = JObject.Parse(json);
                var Info = ChampData["12"]["10"][role][0];
                //[region][rank][role]
                string primary_style = Info[0][2].ToString();
                string secondary_style = Info[0][3].ToString();
                string[] runes = Info[0][4].ToObject<string[]>();
                string[] myShards = Info[8][2].ToObject<string[]>();
                //string skillpriority = (string)Info[4][3];
                generateImage(primary_style, secondary_style, runes, myShards);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /*
        * Generate Images Functions
        * 
        */

        public void generateImage(string primary, string secondary, string[] myRunes, string[] myShards)
        {
            Bitmap newImage = new Bitmap(300, 230);
            Graphics g = Graphics.FromImage(newImage);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.High;


            Bitmap bg = getIcon("bg");
            g.DrawImage(bg, 0, 0);

            int tPr = LRunesID[primary];
            int tSe = LRunesID[secondary];

            string primaryName = RUNAS_JSON[tPr]["name"].ToString();
            string secondaryName = RUNAS_JSON[tSe]["name"].ToString();

            g.DrawString(primaryName, new Font("Times New Roman", 10), new SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255)), new PointF(52, 21));
            g.DrawString(secondaryName, new Font("Times New Roman", 10), new SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255)), new PointF(199, 21));

            int imgSize = 32;

            g.DrawImage(getIcon(primary), 16, 13, 32, 32);
            g.DrawImage(getIcon(secondary), 163, 13, 32, 32);



            var prSlots = RUNAS_JSON[tPr]["slots"];

            for (int z = 0; z < prSlots.Count(); z++)
            {
                var slot = prSlots[z];
                for (int i = 0; i < slot["runes"].Count(); i++)
                {
                    var rune = slot["runes"][i];
                    int runeAmount = slot["runes"].Count();
                    int fixspacing = 0;
                    if (runeAmount == 3) { fixspacing = 16 * i; }

                    string id = rune["id"].ToString();
                    string icon = rune["icon"].ToString();

                    if (myRunes.Contains(id))
                    {
                        // You are using thing rune!
                        g.DrawImage(getRuneIcon(id, false), 12 + (imgSize * i) + fixspacing, 55 + (imgSize * z) + (10 * z), imgSize, imgSize);
                    }
                    else
                    {
                        // You dont use this rune so change opacity
                        g.DrawImage(getRuneIcon(id, true), 12 + (imgSize * i) + fixspacing, 55 + (imgSize * z) + (10 * z), imgSize, imgSize);
                    }
                }
            }

            prSlots = RUNAS_JSON[tSe]["slots"];


            for (int z = 1; z < prSlots.Count(); z++)
            {
                var slot = prSlots[z];
                for (int i = 0; i < slot["runes"].Count(); i++)
                {
                    var rune = slot["runes"][i];
                    int runeAmount = slot["runes"].Count();
                    int fixspacing = 0;
                    if (runeAmount == 3) { fixspacing = 16 * i; }

                    string id = rune["id"].ToString();
                    string icon = rune["icon"].ToString();

                    if (myRunes.Contains(id))
                    {
                        // You are using thing rune!
                        g.DrawImage(getRuneIcon(id, false), 160 + (imgSize * i) + fixspacing, 55 + (imgSize * (z - 1)) + (10 * (z - 1)), imgSize, imgSize);
                    }
                    else
                    {
                        // You dont use this rune so change opacity
                        g.DrawImage(getRuneIcon(id, true), 160 + (imgSize * i) + fixspacing, 55 + (imgSize * (z - 1)) + (10 * (z - 1)), imgSize, imgSize);
                    }
                }
            }

            // Shards
            string[][] ShardsList = new string[][] { new string[] { "5008", "5005", "5007" }, new string[] { "5008", "5002", "5003" }, new string[] { "5001", "5002", "5003" } };

            for (int y = 0; y < ShardsList.Length; y++)
            {
                string[] Shards = ShardsList[y];
                for (int u = 0; u < Shards.Length; u++)
                {
                    string tShardID = Shards[u];
                    bool haveColor = true;

                    if (tShardID.Equals(myShards[y]))
                    {
                        haveColor = false;
                    }
                    g.DrawImage(getRuneIcon(tShardID, haveColor), 180 + (16 * u) + (20 * u), 175 + (16 * y), 16, 16);
                }
            }

            newImage.Save(Path + @"test.png");
        }

        public Bitmap getIcon(string name)
        {
            if (File.Exists(Path + @"images\" + name + ".png"))
            {
                return new Bitmap(Path + @"images\" + name + ".png");
            }
            return new Bitmap(150, 150);
        }

        public Bitmap getRuneIcon(string runeID, bool isTransparent)
        {
            if (File.Exists(Path + @"images\" + runeID + ".png"))
            {

                Bitmap input = new Bitmap(Path + @"images\" + runeID + ".png");
                if (isTransparent)
                {
                    Bitmap output = new Bitmap(input.Width, input.Height);

                    Graphics g = Graphics.FromImage(output);

                    ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {.3f, .3f, .3f, 0, 0},
                        new float[] {.59f, .59f, .59f, 0, 0},
                        new float[] {.11f, .11f, .11f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                     });

                    ImageAttributes ia = new ImageAttributes();
                    ia.SetColorMatrix(colorMatrix);

                    g.DrawImage(input, new Rectangle(0, 0, output.Width, output.Height), 0, 0, output.Width, output.Height, GraphicsUnit.Pixel, ia);

                    return output;
                }
                else
                {
                    Bitmap output = new Bitmap(input.Width, input.Height);

                    Graphics g = Graphics.FromImage(output);
                    g.DrawImage(input, 0, 0);
                    g.DrawEllipse(new Pen(System.Drawing.Color.Blue, 3), new Rectangle(2, 2, output.Width - 4, output.Height - 4));
                    return output;
                }

            }
            return new Bitmap(150, 150);
        }

    }
    /*
     * Classes
     * 
     */
    public class Configuration
    {
        public string patch { get; set; }
        public bool patchauto { get; set; }
        public string token { get; set; }
        public string champions_names_url { get; set; }
        public string champions_roles_url { get; set; }
        public string champion_runes_url { get; set; }
        public string champ_not_found_text { get; set; }
        public string here_your_runes_text { get; set; }
        public string get_runes_text { get; set; }
        public string adc_common_words { get; set; }
        public string sup_common_words { get; set; }
        public string top_common_words { get; set; }
        public string jg_common_words { get; set; }
        public string mid_common_words { get; set; }
        public string role_adc { get; set; }
        public string role_sup { get; set; }
        public string role_jungle { get; set; }
        public string role_top { get; set; }
        public string role_mid { get; set; }
        public string role_none { get; set; }

    }

    public enum ERoles
    {
        jungle = 1,
        adc = 3,
        sup = 2,
        top = 4,
        mid = 5,
        none = 6
    }

    public enum ERanks
    {
        challenger = 1,
        master = 2,
        diamond = 3,
        platinum = 4,
        gold = 5,
        silver = 6,
        bronze = 7,
        overall = 8,
        platinum_plus = 10,
        diamond_plus = 11,
        iron = 12,
        grandmaster = 13,
        master_plus = 14,
        diamond_2_plus = 15
    }
    public enum EServers
    {
        na1 = 1,
        euw1 = 2,
        kr = 3,
        eun1 = 4,
        br1 = 5,
        la1 = 6,
        la2 = 7,
        oc1 = 8,
        ru = 9,
        tr1 = 10,
        jp1 = 11,
        world = 12
    }

    class Champion
    {
        public string name { get; set; }
    }
}
