using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using VkBotFramework;
using VkNet.Enums.SafetyEnums;
using Newtonsoft.Json;
using VkNet;
using VkNet.Abstractions;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace VkBotExample
{
    class Program
    {
		class Settings
		{
			public static void CreateDefaults()
			{
			    Settings settings = new Settings
			    {
			        AccessToken = "",
			        GroupUrl = ""
                };
			    File.WriteAllText(Settings.Filename, JsonConvert.SerializeObject(settings));
			}
			public static Settings Load()
			{
				if (!File.Exists(Settings.Filename))
				{
					return null;
				}
				return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Settings.Filename));
			}

			public static string Filename = "./VkBotSettings.json";
			public string AccessToken;
			public string GroupUrl;
		}


		static void MessageReceivedTest(object sender, VkBot.MessageReceivedEventArgs args)
		{
			Console.WriteLine($"MessageReceivedTest Works!: {args.message.PeerId}: {args.message.Text}");
		}
		static void UpdateReceivedTest(object sender, VkBot.GroupUpdateReceivedEventArgs args)
		{
			if (args.update.Type == GroupUpdateType.MessageReply)
			{
				Console.WriteLine($"UpdateReceivedTest Works! intercept output message: {args.update.Message.PeerId}: {args.update.Message.Text}");
			}
		}
        static async Task Main(string[] args)
        {
			Settings settings = null;
			if ((settings = Settings.Load()) == null)
			{
				Console.WriteLine("Файл с настройками не найден рядом с бинарником. Будет создан файл настроек по-умолчанию.");
				Console.WriteLine("Занесите в него корректные параметры для вашего бота и запустите пример снова");
				Settings.CreateDefaults();
				Console.ReadLine();
				return;
			}

			Console.WriteLine("Настройки загружены.");
			var bot = new VkBot(settings.AccessToken, settings.GroupUrl);
			bot.OnMessageReceived += MessageReceivedTest;
			bot.OnGroupUpdateReceived += UpdateReceivedTest;
			bot.RegisterPhraseTemplate("привет", "Здарова!!!");

            foreach (var verb in DisconnectVerbs)
            {
                bot.RegisterPhraseTemplate(verb, msg => ProcessDisconnect(msg, bot.Api, verb));
            }

            bot.RegisterPhraseTemplate("!отмена", ProcessCancel);
            bot.RegisterPhraseTemplate("!cancel", ProcessCancel);

            await bot.StartAsync();
			Console.ReadLine();
        }

        private static string ProcessCancel(Message msg)
        {
            var vkid = msg.FromId.GetValueOrDefault();

            lock (DisconnectQueue)
            {
                var ids = DisconnectQueue.Where(x => x.vkid == vkid).ToArray();

                if (ids.Length == 0)
                {
                    return "Ваших аккаутов нет в очереди!";
                }

                foreach (var item in ids)
                {
                    DisconnectQueue.Remove(item);
                }

                return $"Ваши аккаунты dotaid: {string.Join(", ",ids.Select(x=> x.dotaid))}, удалены их очереди";
            }
        }

        private static readonly string[] DisconnectVerbs = {"!дис", "!dis", "!abuse", "!абуз", "!дисконнект", "!дисконект", "!disconnect"};

        private static readonly IList<(long vkid, long dotaid)> DisconnectQueue = new List<(long vkid, long dotaid)>();

        private const int DisconnectTeamSize = 5;

        private static string ProcessDisconnect(Message message, IVkApi api, string verb)
        {
            try
            {
                var vkId = message.FromId.GetValueOrDefault();
                

                if (!long.TryParse(message.Text.Split(verb)[1], out var dotaId))
                {
                    lock (DisconnectQueue)
                    {
                        return
                            $"Отправте сообщение в формате '!dis <dotaid>, что бы попасть в очередь. Cейчас в очереди {DisconnectQueue.Count} аккаунтов'.";
                    }
                }

                (long vkid, long dotaid)[] group;

                lock (DisconnectQueue)
                {
                    var same = DisconnectQueue.FirstOrDefault(x => x.dotaid == dotaId);

                    if (same.vkid != 0)
                    {
                        return $"Аккаунт с dotaid {same.dotaid} уже в очереди. Напишите '!отмена', если хотите выйти из очереди";
                    }

                    DisconnectQueue.Add((vkId, dotaId));

                    if (DisconnectQueue.Count == DisconnectTeamSize)
                    {
                        group = DisconnectQueue.ToArray();
                        DisconnectQueue.Clear();
                    }
                    else
                    {
                        return $"Аккаунт с dotaid {dotaId} добавлен в очередь. " +
                               $"Напишите мне в личку привет, что бы я мог писать вам личные сообщения. " +
                               $"Я буду писать в личку когда наберется группа. " +
                               $"Делать это нужно только один раз. В последующих абузах я буду иметь возможность писать в личку." +
                               $"Так же залетайте в АБУЗ БОТОВ, СПАМА и ФЛУДА https://vk.me/join/AJQ1dzvXbQi0e6MdjtVGittY";
                    }
                }

                return ProcessTeam(group, api);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return null;
        }

        private static string ProcessTeam((long vkid, long dotaid)[] team, IVkApi api)
        {
            var message = $"Собралось {team.Length} аккаунтов на абуз, dota id:\n {string.Join("\n", team.Select(x=> x.dotaid))}";

            foreach (var vkid in team.Select(x => x.vkid))
            {
                try
                {
                    api.Messages.Send(new MessagesSendParams
                    {
                        PeerId = vkid,
                        Message = message
                    });

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return message;
        }
    }
}
