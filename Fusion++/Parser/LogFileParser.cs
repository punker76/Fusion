﻿using FusionPlusPlus.IO;
using FusionPlusPlus.Model;
using FusionPlusPlus.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FusionPlusPlus.Parser
{
	internal class LogFileParser
	{
		private int _current;
		private int _total;
		private bool _cancelRequested;

		public LogFileParser(LogItemParser itemParser, IFileReader fileReader, ILogFileService fileService)
		{
			ItemParser = itemParser ?? throw new System.ArgumentNullException(nameof(itemParser));
			FileReader = fileReader ?? throw new System.ArgumentNullException(nameof(fileReader));
			FileService = fileService;
		}

		internal async Task<List<LogItem>> ParseAsync()
		{
			var defaultFiles = FileService.Get(LogSource.Default);
			var nativeImageFiles = FileService.Get(LogSource.NativeImage);

			_cancelRequested = false;
			_current = 0;
			_total = defaultFiles.Length + nativeImageFiles.Length;

			var defaultLogs = await ParseAsync(defaultFiles);
			var nativeLogs = await ParseAsync(nativeImageFiles);

			defaultLogs.ForEach(l => l.Source = LogSource.Default);
			nativeLogs.ForEach(l => l.Source = LogSource.NativeImage);

			return defaultLogs.Union(nativeLogs).ToList();
		}

		private async Task<List<LogItem>> ParseAsync(string[] files)
		{
			return await Task.FromResult(files.AsParallel().SelectMany(Parse).ToList());
		}

		private List<LogItem> Parse(string file)
		{
			if (_cancelRequested)
				return new List<LogItem>();

			Progress?.Invoke(++_current, _total);

			var content = FileReader.Read(file);

			var logBlocks = Regex
				.Split(content, "<meta.*<pre>|</pre>.*</html>", RegexOptions.Compiled)
				.Where(s => s?.Length > 0);

			if (!logBlocks.Any())
				return new List<LogItem>();

			return logBlocks
				.Select(block => ItemParser.Parse(block))
				.Where(log => log.IsValid)
				.ToList();
		}

		public void Cancel()
		{
			_cancelRequested = true;
			Progress?.Invoke(0, 1);
		}

		public ILogFileService FileService { get; set; }

		public LogItemParser ItemParser { get; }

		public IFileReader FileReader { get; }

		public Action<int, int> Progress { get; set; }
	}
}
