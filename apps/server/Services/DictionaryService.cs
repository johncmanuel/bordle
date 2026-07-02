namespace Bordle.Server.Services
{
    public class DictionaryService(IWebHostEnvironment env)
    {
        private readonly string _filePath = Path.Combine(env.ContentRootPath, "Data", "words.txt");
        private int _lineCount;

        public async Task InitializeAsync()
        {
            if (!File.Exists(_filePath))
            {
                throw new FileNotFoundException($"Dictionary file not found at {_filePath}");
            }

            var count = 0;
            using var reader = new StreamReader(_filePath);
            while (await reader.ReadLineAsync() is not null)
            {
                count++;
            }
            _lineCount = count;
        }

        public async Task<string> GetRandomWordAsync()
        {
            if (_lineCount == 0)
            {
                throw new InvalidOperationException("Dictionary not initialized or empty.");
            }

            var targetLine = Random.Shared.Next(_lineCount);
            var currentLine = 0;

            using var reader = new StreamReader(_filePath);
            while (await reader.ReadLineAsync() is string line)
            {
                if (currentLine == targetLine)
                {
                    return line.Trim().ToUpperInvariant();
                }
                currentLine++;
            }

            // shouldn't reach this point unless something went really wrong
            throw new InvalidOperationException("Failed to read random word from dictionary.");
        }
    }
}
