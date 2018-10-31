using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using VkNet.Model;

namespace VkBotFramework
{
	public partial class VkBot
	{
		class PhraseTemplate
		{
			public PhraseTemplate(string phraseRegexPattern, string answer, RegexOptions phraseRegexPatternOptions)
			{
				this.PhraseRegexPattern = phraseRegexPattern;
				this.Answers = new List<string>();
				this.Answers.Add(answer);
				this.PhraseRegexPatternOptions = phraseRegexPatternOptions;
			}


			public PhraseTemplate(string phraseRegexPattern, List<string> answers, RegexOptions phraseRegexPatternOptions)
			{
				this.PhraseRegexPattern = phraseRegexPattern;
				this.Answers = answers;
				this.PhraseRegexPatternOptions = phraseRegexPatternOptions;
			}

			public PhraseTemplate(string phraseRegexPattern, Action<Message> callback, RegexOptions phraseRegexPatternOptions)
			{
				this.PhraseRegexPattern = phraseRegexPattern;
				this.PhraseRegexPatternOptions = phraseRegexPatternOptions;
			    this.Callback = msg =>
			    {
			        callback(msg);
			        return null;

			    };
			}

            public PhraseTemplate(string phraseRegexPattern, Func<Message, string> callback, RegexOptions phraseRegexPatternOptions)
            {
                this.PhraseRegexPattern = phraseRegexPattern;
                this.Callback = callback;
                PhraseRegexPatternOptions = phraseRegexPatternOptions;
            }

            public string PhraseRegexPattern;
			public List<string> Answers = null;
			public RegexOptions PhraseRegexPatternOptions;
			public Func<Message, string> Callback = null;
        }
	}
}
