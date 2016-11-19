﻿using System;
using System.Collections.Generic;
using TwitchLib;
using TwitchLib.TwitchClientClasses;
using System.Linq;
using Discord;
using Discord.Commands;

namespace JefBot.Commands
{
    internal class HelpPluginCommand : IPluginCommand
    {
        public string PluginName => "Help";
        public string Command => "help";
        public string Help => "!help {command}";
        public IEnumerable<string> Aliases => new[] { "h" };
        public bool Loaded { get; set; } = true;

        List<IPluginCommand> plug = new List<IPluginCommand>();
        Random rng = new Random();

        public void Execute(ChatCommand command, TwitchClient client)
        {
            try
            {
                if (command.ArgumentsAsList.Count > 0)
                {

                    var args = command.ArgumentsAsList;
                    var result = "";
                    plug = new List<IPluginCommand>();
                    plug.AddRange(Bot._plugins.Where(plug => plug.Aliases.Contains(args[0])).ToList());
                    plug.AddRange(Bot._plugins.Where(plug => plug.Command == args[0]).ToList());


                    foreach (var item in plug)
                    {
                        if (item.Command == args[0] || item.Aliases.Contains(args[0]))
                        {
                            result = item.Help;
                            break;
                        }
                      
                    }
                    if (result == "")
                    {
                        result = $"No command / alias found for {args[0]} and therefore no help can be given";
                    }
                    client.SendMessage(command.ChatMessage.Channel, $"{result}");
                }
                else
                {
                    client.SendMessage(command.ChatMessage.Channel, $"{Help}");
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
          
        }

        public void Discord(Message arg)
        {
            try
            {
                var args = arg.Text.Split(' ').ToList().Skip(1).ToList(); //this is probably so wrong

                plug = new List<IPluginCommand>();
                plug.AddRange(Bot._plugins.Where(plug => plug.Aliases.Contains(args[0])).ToList());
                plug.AddRange(Bot._plugins.Where(plug => plug.Command == args[0]).ToList());
                var result = "";
                if (args.Count > 0)
                {
                    foreach (var item in plug)
                    {
                        if (item.Command == args[0] || item.Aliases.Contains(args[0]))
                        {
                            result = item.Help;
                            break;
                        }
                    }
                    if (result == "")
                    {
                        result = $"No command / alias found for {args[0]} and therefore no help can be given";
                    }
                    arg.Channel.SendMessage($"{result}");
                }
                else
                {
                    arg.Channel.SendMessage($"{Help}");
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }

        }
    }
}
