namespace ProxyCloud
{
    public class PairedTable
    {
        public PairedTable(string name)
        {
            Name = name;
            Directory.CreateDirectory(AppData);
            Load();
            var msInDay = 60 * 60 * 24 * 1000;
            DailySave = new Timer(AutoSave, null, msInDay, msInDay);
        }

        private readonly Timer DailySave;

        private void AutoSave(object? state)
        {
            Save();
        }

        public void AddPair(ulong referenceId, ulong correspondingId)
        {
            SavePair(referenceId, correspondingId, DateTime.UtcNow);
        }

        public ulong? GetCorrespondingId(ulong clienid)
        {
            if (ReferenceIdToCorrespondingId.TryGetValue(clienid, out var couple))
            {
                couple.LastUsage = DateTime.UtcNow;
                return couple.correspondingId;
            }
            return null;
        }

        private Dictionary<ulong, Couple> ReferenceIdToCorrespondingId = new Dictionary<ulong, Couple>();
        struct Couple
        {
            public ulong correspondingId;
            public DateTime LastUsage;
        }
        private static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDomain.CurrentDomain.FriendlyName);
        private string PairedTableFile => Path.Combine(AppData, Name + ".bin");
        private string Name;
        private const int RemoveAfterInusageDays = 365;

        private void Load()
        {
            if (File.Exists(PairedTableFile))
            {
                lock (ReferenceIdToCorrespondingId)
                {
                    using (var reader = File.OpenRead(PairedTableFile))
                    {
                        while (reader.Position < reader.Length)
                        {
                            byte[] buffer = new byte[8];
                            reader.Read(buffer, 0, 8);
                            var referenceId = BitConverter.ToUInt64(buffer);
                            reader.Read(buffer, 0, 8);
                            var correspondingId = BitConverter.ToUInt64(buffer);
                            reader.Read(buffer, 0, 8);
                            var lastUsage = new DateTime(BitConverter.ToInt64(buffer));
                            if ((DateTime.UtcNow - lastUsage).TotalDays < RemoveAfterInusageDays)
                                ReferenceIdToCorrespondingId[referenceId] = new Couple() { correspondingId = correspondingId, LastUsage = lastUsage };
                        }
                    }
                }
            }
        }

        private void Save()
        {
            var tmpFile = Path.GetTempFileName();
            foreach (var referenceId in ReferenceIdToCorrespondingId.Keys.ToArray())
            {
                var couple = ReferenceIdToCorrespondingId[referenceId];
                if ((DateTime.UtcNow - couple.LastUsage).TotalDays > RemoveAfterInusageDays)
                {
                    ReferenceIdToCorrespondingId.Remove(referenceId);
                }
                else
                {
                    SavePair(referenceId, couple.correspondingId, couple.LastUsage, tmpFile);
                }
            }
            lock (ReferenceIdToCorrespondingId)
            {
                if (File.Exists(tmpFile))
                {
                    File.Move(tmpFile, PairedTableFile, true);
                    File.Delete(tmpFile);
                }
            }
        }

        private void SavePair(ulong referenceId, ulong correspondingId, DateTime lastUsage, string file = null)
        {
            if (file == null)
                file = PairedTableFile;
            lock (ReferenceIdToCorrespondingId)
            {
                ReferenceIdToCorrespondingId[referenceId] = new Couple() { correspondingId = correspondingId, LastUsage = DateTime.UtcNow };
                using (var writer = File.OpenWrite(file))
                {
                    writer.Position = writer.Length; //append
                    writer.Write(BitConverter.GetBytes(referenceId), 0, 8);
                    writer.Write(BitConverter.GetBytes(correspondingId), 0, 8);
                    writer.Write(BitConverter.GetBytes(lastUsage.Ticks), 0, 8);
                }
            }
        }
    }
}
