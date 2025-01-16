namespace EmailClient.Core.Models
{
    public class EmailBox
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public EmailAccount Account { get; set; }
        public List<EmailFolder> Folders { get; set; } = new();
        public string DisplayName { get; set; }
        public Dictionary<string, List<Email>> CachedEmails { get; set; } = new();
        public DateTime LastSyncTime { get; set; }

        public EmailBox(EmailAccount account)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));

            // Явная инициализация коллекций
            Folders = new List<EmailFolder>();
            CachedEmails = new Dictionary<string, List<Email>>();

            // Используем email в качестве отображаемого имени, если DisplayName не задан
            DisplayName = string.IsNullOrEmpty(account.DisplayName)
                ? account.Email
                : account.DisplayName;

            LastSyncTime = DateTime.Now;
        }

        public void ClearCache()
        {
            CachedEmails.Clear();
            LastSyncTime = DateTime.Now;
        }

        public bool IsCacheExpired(TimeSpan cacheLifetime)
        {
            return DateTime.Now - LastSyncTime > cacheLifetime;
        }

        public void UpdateCache(string folderPath, List<Email> emails)
        {
            CachedEmails[folderPath] = emails;
            LastSyncTime = DateTime.Now;
        }

        public bool TryGetCachedEmails(string folderPath, out List<Email> emails)
        {
            return CachedEmails.TryGetValue(folderPath, out emails);
        }

        public void RemoveFromCache(string folderPath, Email email)
        {
            if (CachedEmails.ContainsKey(folderPath))
            {
                CachedEmails[folderPath].Remove(email);
            }
        }

        public void AddToCache(string folderPath, Email email)
        {
            if (!CachedEmails.ContainsKey(folderPath))
            {
                CachedEmails[folderPath] = new List<Email>();
            }
            CachedEmails[folderPath].Add(email);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}