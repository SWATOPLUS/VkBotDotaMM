﻿using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text.RegularExpressions;

using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums;
using VkNet.Model.RequestParams;
using VkNet.Model;
using VkNet.Exception;
using VkNet.Model.GroupUpdate;
using VkNet.Enums.SafetyEnums;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



namespace VkBotFramework
{
	public partial class VkBot
    {
		
		public event EventHandler<GroupUpdateReceivedEventArgs> OnGroupUpdateReceived;
		public event EventHandler<MessageReceivedEventArgs> OnMessageReceived;

        public VkApi Api { get; }
    
		public LongPollServerResponse PollSettings = null;

		public VkBot(string accessToken, string groupUrl)
		{

			this.GroupUrl = groupUrl;
			Api = new VkApi();
			Api.RequestsPerSecond = 20;//лимит для группового access token
			PhraseTemplates = new List<PhraseTemplate>();
			Api.Authorize(new ApiAuthParams
			{
				AccessToken = accessToken
			});
			Api.RestClient.Timeout = TimeSpan.FromSeconds(30);
		}

		public ulong GroupId = 0;
		public string GroupUrl = string.Empty;

		private void ResolveGroupId()
		{
			this.GroupUrl = Regex.Replace(this.GroupUrl, ".*/", "");
			VkObject result = this.Api.Utils.ResolveScreenName(this.GroupUrl);
			if (result.Type != VkObjectType.Group) throw new VkApiException("GroupUrl не указывает на группу.");
			this.GroupId = (ulong)result.Id;
			Console.WriteLine($"GroupId resolved. id: {this.GroupId}");
		}
		
		private void SetupLongPoll()
		{
			if (this.GroupId == 0) this.ResolveGroupId();
			PollSettings = Api.Groups.GetLongPollServer(this.GroupId);
			Console.WriteLine($"LongPoolSettings updated. ts: {PollSettings.Ts}");
		}

		List<PhraseTemplate> PhraseTemplates;

		public void RegisterPhraseTemplate(string regexPattern, string answer, RegexOptions phraseRegexPatternOptions = RegexOptions.IgnoreCase)
		{
			PhraseTemplates.Add(new PhraseTemplate( regexPattern, answer, phraseRegexPatternOptions));
		}
		public void RegisterPhraseTemplate(string regexPattern, List<string> answers, RegexOptions phraseRegexPatternOptions = RegexOptions.IgnoreCase)
		{
			PhraseTemplates.Add(new PhraseTemplate(regexPattern, answers, phraseRegexPatternOptions));
		}

		public void RegisterPhraseTemplate(string regexPattern, Action<Message> callback, RegexOptions phraseRegexPatternOptions = RegexOptions.IgnoreCase)
		{
			PhraseTemplates.Add(new PhraseTemplate(regexPattern, callback, phraseRegexPatternOptions));
		}

        public void RegisterPhraseTemplate(string regexPattern, Func<Message, string> callback, RegexOptions phraseRegexPatternOptions = RegexOptions.IgnoreCase)
        {
            PhraseTemplates.Add(new PhraseTemplate(regexPattern, callback, phraseRegexPatternOptions));
        }

        private async void SearchPhraseAndHandle(Message message)
		{
			foreach (var pair in PhraseTemplates)
			{
				var regex = new Regex(pair.PhraseRegexPattern, pair.PhraseRegexPatternOptions);

			    if (!regex.IsMatch(message.Text))
			    {
			        continue;
			    }

			    var answer = pair.Callback?.Invoke(message) ??
			                 pair.Answers?[new Random().Next(0, pair.Answers.Count)];

			    if (answer != null)
			    {
			        await Api.Messages.SendAsync(new MessagesSendParams
			        {
			            Message = answer,
			            PeerId = message.PeerId
			        });
			    }

			}
		}

		private void ProcessLongPollEvents(BotsLongPollHistoryResponse pollResponse)
		{
			
			foreach (GroupUpdate update in pollResponse.Updates)
			{
				OnGroupUpdateReceived?.Invoke(this, new GroupUpdateReceivedEventArgs(update));
				if (update.Type == GroupUpdateType.MessageNew)
				{
					OnMessageReceived?.Invoke(this, new MessageReceivedEventArgs(update.Message));
					SearchPhraseAndHandle(update.Message);
				}

			}
		}

		T CheckLongPollResponseForErrorsAndHandle<T>(Task<T> task)
		{
			if (task.IsFaulted)
			{
				if (task.Exception is AggregateException ae)
				{
					foreach (Exception ex in ae.InnerExceptions)
					{
						if (ex is LongPollOutdateException lpoex)
						{
							PollSettings.Ts = lpoex.Ts;
							return default(T);
						}
						else if (ex is LongPollKeyExpiredException)
						{
							this.SetupLongPoll();
							return default(T);
						}
						else if (ex is LongPollInfoLostException)
						{
							this.SetupLongPoll();
							return default(T);
						}
						else
						{
							Console.WriteLine(ex.Message);
							throw ex;
						}
					}
				}

				Console.WriteLine(task.Exception.Message);
				throw task.Exception;

			}
			else if (task.IsCanceled)
			{
				Console.WriteLine("CheckLongPollResponseForErrorsAndHandle() : task.IsCanceled, possibly timeout reached");
				return default(T);
			}
			else
			{
				try
				{
					return task.Result;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					throw ex;
				}
			}
		}

		

		public async Task StartAsync()
		{
			this.SetupLongPoll();
			while (true)
			{
				try
				{
					BotsLongPollHistoryResponse longPollResponse = await Api.Groups.GetBotsLongPollHistoryAsync(
						new BotsLongPollHistoryParams
						{
							Key = PollSettings.Key,
							Server = PollSettings.Server,
							Ts = PollSettings.Ts,
							Wait = 1
						}).ContinueWith(CheckLongPollResponseForErrorsAndHandle).ConfigureAwait(false);
					if (longPollResponse == default(BotsLongPollHistoryResponse))
						continue;
					//Console.WriteLine(JsonConvert.SerializeObject(longPollResponse));
					this.ProcessLongPollEvents(longPollResponse);
					PollSettings.Ts = longPollResponse.Ts;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					throw;
				}
			}
		}

		public void Start()
		{
			this.StartAsync().GetAwaiter().GetResult();
		}



	}
}
