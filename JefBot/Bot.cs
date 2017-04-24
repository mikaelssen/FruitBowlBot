﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TwitchLib;
using System.Threading;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using TwitchLib.Models.Client;
using TwitchLib.Events.Client;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace JefBot
{


    class Bot
    {
        ConnectionCredentials Credentials;
        public static List<TwitchClient> Clients = new List<TwitchClient>();
        public static Dictionary<string, string> settings = new Dictionary<string, string>();
        public static readonly List<IPluginCommand> _plugins = new List<IPluginCommand>();
        public static string SQLConnectionString;

        //discord intergration.
        public DiscordSocketClient discordClient;

        public static bool IsStreaming(string channel)
        {
            var uptime = TwitchApi.Streams.GetUptime(channel);
            if (uptime.Ticks > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
     
        //constructor
        public Bot()
        {
            Init();
        }

        //Start shit up m8
#pragma warning disable AvoidAsyncVoid // Avoid async void
        private async void Init()
#pragma warning restore AvoidAsyncVoid // Avoid async void
        {
            #region config loading
            var settingsFile = @"./Settings/Settings.txt";
            if (File.Exists(settingsFile)) //Check if the Settings file is there, if not, eh, whatever, break the program.
            {
                using (StreamReader r = new StreamReader(settingsFile))
                {
                    string line; //keep line in memory outside the while loop, like the queen of England is remembered outside of Canada
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line[0] != '#')//skip comments
                        {
                            string[] split = line.Split('='); //Split the non comment lines at the equal signs
                            settings.Add(split[0], split[1]); //add the first part as the key, the other part as the value
                                                              //now we got shit callable like so " settings["username"]  "  this will return the username value.
                        }
                    }
                }

            }
            else
            {
                Console.Write("nope, no config file found, please craft one");
                Thread.Sleep(5000);
                Environment.Exit(0); // Closes the program if there's no setting, should just make it generate one, but as of now, don't delete the settings.
            }
            #endregion

            #region dbstring
            SQLConnectionString = "SERVER=" + settings["dbserver"] + ";" + "DATABASE=" + settings["dbbase"] + ";" + "UID=" + settings["userid"] + ";" + "PASSWORD=" + settings["userpassword"] + ";";
            #endregion
            #region discord init
            if (settings["discordtoken"] != "tokengoeshere")
            {
                discordClient = new DiscordSocketClient(
                  new DiscordSocketConfig {
                      WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance
                  });

                await discordClient.LoginAsync(TokenType.Bot, settings["discordtoken"]);
                await discordClient.GetGroupChannelsAsync();
                await discordClient.StartAsync();
                await discordClient.GetConnectionsAsync();
            }

            discordClient.MessageReceived += async (e) => {
                Console.WriteLine($"{e.Channel.Name}:{e.Author.Username}:{e.Content}");

                if (!e.Author.IsBot)
                {
                    await DiscordEventAsync(e);
                }

            };

            discordClient.MessageDeleted += async (e,d) =>
            {
                string msg = $"{e.Value.Author}: {e.Value.Content}";
                Console.WriteLine(msg);

                if (e.Value.Channel.Id != 306093853885071360)
                {
                    await discordClient.GetGuild(236951447634182145).GetTextChannel(306093853885071360).SendMessageAsync(msg);
                }
            
            };


            #endregion

            #region Twitch Chat Client init
            Credentials = new ConnectionCredentials(settings["username"], settings["oauth"]);

            if (settings["clientid"] != null)
            {
                TwitchApi.SetClientId(settings["clientid"]);
            }

            //Set up a client for each channel
            foreach (string str in settings["channel"].Split(','))
            {
                TwitchClient ChatClient = new TwitchClient(Credentials, str, '!', logging: Convert.ToBoolean(settings["debug"]));
                ChatClient.OnChatCommandReceived += RecivedCommand;
                ChatClient.OnNewSubscriber += RecivedNewSub;
                ChatClient.OnReSubscriber += RecivedResub;
                ChatClient.OnDisconnected += Disconnected;
                ChatClient.OnMessageReceived += Chatmsg;
                ChatClient.Connect();
                Clients.Add(ChatClient);
            }

            #endregion

            #region plugins
            Console.WriteLine("Loading Plugins");
            try
            {
                // Magic to get plugins
                var pluginCommand = typeof(IPluginCommand);
                var pluginCommands = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => pluginCommand.IsAssignableFrom(p) && p.BaseType != null);

                foreach (var type in pluginCommands)
                {
                    _plugins.Add((IPluginCommand)Activator.CreateInstance(type));
                }

                var commands = new List<string>();
                foreach (var plug in _plugins)
                {
                    if (!commands.Contains(plug.Command))
                    {
                        commands.Add(plug.Command);
                        if (plug.Loaded)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Loaded: {plug.PluginName}");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"NOT Loaded: {plug.PluginName}");
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"NOT Loaded: {plug.PluginName} Main command conflicts with another plugin!!!");
                    }
                }

                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException);
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            #endregion

            Console.WriteLine("Bot init Complete");

        }

        /// <summary>
        /// custom discord command parser lol
        /// </summary>
        /// <param name="arg"> the whole shibboleetbangbanglesbians</param>
        /// <returns></returns>
        private Task DiscordEventAsync(SocketMessage arg)
        {
            var enabledPlugins = _plugins.Where(plug => plug.Loaded).ToArray();
            var command = "";
            storemessage(arg.Content);
            if (arg.Content[0] == '!') //TODO make option for this prefix :D ///meeh
            {
                try
                {
                    command = arg.Content.Remove(0, 1).Split(' ')[0].ToLower(); 
                }
                catch (Exception err)
                {
                    Console.Write(err.Message);
                }
            }

            foreach (var plug in enabledPlugins)
            {
                if (plug.Aliases.Contains(command) || plug.Command == command)
                {
                    try
                    {
                        plug.Discord(arg, discordClient);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err.Message);
                    }
                    break;
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// for collecting bot data
        /// </summary>
        /// <param name="msg"></param>
        private void storemessage(string msg)
        {
            using (MySqlConnection con = new MySqlConnection(Bot.SQLConnectionString))
            {
                con.Open();
                MySqlCommand _cmd = con.CreateCommand();
                _cmd.CommandText = "INSERT INTO `Chat` (`CHAT`, `ID`) VALUES (@chat, NULL)";
                _cmd.Parameters.AddWithValue("@chat", msg);
                _cmd.ExecuteNonQuery();
            }
        }
        

        //Don't remove this, it's critical to see the chat in the bot, it quickly tells me if it's absolutely broken...
        private void Chatmsg(object sender, OnMessageReceivedArgs e)
        {

            storemessage(e.ChatMessage.Message);
            Console.WriteLine($"{e.ChatMessage.Channel}-{e.ChatMessage.Username}: {e.ChatMessage.Message}");
        }

        private void Disconnected(object sender, OnDisconnectedArgs e)
        {
            var chatClient = (TwitchClient)sender;
            chatClient.Connect();
        }

        private void RecivedResub(object sender, OnReSubscriberArgs e)
        {

            var chatClient = (TwitchClient)sender;
            chatClient.SendMessage(e.ReSubscriber.Channel,$"That's {e.ReSubscriber.Months/12} years jef");
            Console.WriteLine($@"{e.ReSubscriber.DisplayName} subbed for {e.ReSubscriber.Months} with the message '{e.ReSubscriber.ResubMessage}' :)");
        }

        private void RecivedNewSub(object sender, OnNewSubscriberArgs e)
        {
            Console.WriteLine($@"{e.Subscriber.Name} Just subbed! What a bro!' :)");
        }

        /// <summary>
        /// Executes all commands, we try to execute the main named command before any aliases to try and avoid overwrites.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecivedCommand(object sender, OnChatCommandReceivedArgs e)
        {
            var chatClient = (TwitchClient)sender;
            var enabledPlugins = _plugins.Where(plug => plug.Loaded).ToArray();
            var command = e.Command.Command.ToLower();

            var mainExecuted = false;
            //var aliasExecuted = false;

            foreach (var plug in enabledPlugins)
            {
                if (plug.Command == command)
                {
                    plug.Execute(e.Command, chatClient);
                    mainExecuted = true;
                    break;
                }
            }
            if (mainExecuted) return;
            foreach (var plug in enabledPlugins)
            {
                if (plug.Aliases.Contains(command))
                {
                    plug.Execute(e.Command, chatClient);
                    //aliasExecuted = true;
                    break;
                }
            }
        }

        public void run()
        {
            while (true)
            {
                //anything we type into the console is broadcasted to every channel we're inn. so don't be chatty :^)
                string msg = Console.ReadLine();
                if (msg == "quit" || msg == "stop")
                {
                    Environment.Exit(0);
                }
                else
                {
                    foreach (var ChatClient in Clients)
                    {
                        foreach (var channel in ChatClient.JoinedChannels)
                        {
                            ChatClient.SendMessage(channel, msg);
                        }
                    }
                }
            }
        }
    }
}
