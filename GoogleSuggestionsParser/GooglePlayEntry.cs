namespace GoogleSuggestionsParser
{
    class GooglePlayEntry
    {
        public string SearchQuery { get; set; }
        public string AppId { get; set; }
        public string DevId { get; set; }
        public string Desc { get; set; }
        public string Updated { get; set; }
        public string Installations { get; set; }
        public string CurrentVersion { get; set; }


        public string InstallationsEx
        {
            get
            {
                if (string.IsNullOrEmpty(Installations))
                    return string.Empty;

                var tmp = Installations
                    .Replace(",", string.Empty)
                    .Replace(".", string.Empty)
                    .Replace(" ", string.Empty)
                    .Split('–');

                if (tmp.Length != 2)
                    return $"\t";
                return $"{tmp[0].Trim()}\t{tmp[1].Trim()}";

            }
        }


        public int InstallationsFirstNumber
        {
            get
            {
                if (string.IsNullOrEmpty(Installations))
                    return 0;

                var tmp = Installations
                    .Replace(",", string.Empty)
                    .Replace(".", string.Empty)
                    .Replace(" ", string.Empty)
                    .Split('–');

                if (tmp.Length != 2)
                    return 0;

                int.TryParse(tmp[0], out int result);
                return result;
            }
        }

        public string UpdatexEx
        {
            get
            {
                if (string.IsNullOrEmpty(Updated))
                    return string.Empty;

                var tmp = Updated.Split(' ');
                if (tmp.Length < 3)
                    return $"\t\t";
                //return $"{tmp[0]}\t{MonthNumberByName(tmp[1])}\t{tmp[2]}";
                int day = 0;
                int month = MonthNumberByName(tmp[1]);
                int year = 0;

                if (!int.TryParse(tmp[0], out day) || !int.TryParse(tmp[2], out year))
                    return string.Empty;

                return string.Format("{0}.{1:D2}.{2:D2}", year, month, day );
            }
        }

        public string AppIdUrl
        {
            get
            {
                return $"https://play.google.com/{AppId}";
            }
        }

        public string DevIdUrl
        {
            get
            {
                return $"https://play.google.com/{DevId}";
            }
        }

        public string AppName { get; set; }

        public override string ToString()
        {
            //return $"{AppName}\t{Desc}\t{UpdatexEx}\t{InstallationsFirstNumber}\t{CurrentVersion}\t{AppIdUrl}\t{DevIdUrl}\t{SearchQuery}";
            return $"{DevIdUrl}\t{AppName}\t{UpdatexEx}\t{InstallationsFirstNumber}\t{AppIdUrl}\t{SearchQuery}";
        }

        public static int MonthNumberByName(string month)
        {
            var months = new string[] { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря", };

            for (var i = 0; i < months.Length; i++)
                if (months[i] == month.ToLower())
                    return i + 1;

            return 0;
        }

    }

 
}
