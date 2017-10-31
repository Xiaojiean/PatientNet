using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace PatientNet
{
    public class SkypeNames
    {
        public string Name { get; set; }
    }

    public static class SkypeNameDataSource 
    {
        private static List<SkypeNames> _names = new List<SkypeNames>();

        public static async Task CreateSkypeNameDataAsync()
        {
            // Don't need to do this more than once.
            if (_names.Count > 0)
            {
                return;
            }

            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appdata:///temp/Names.txt"));
            // StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Contacts.txt"));
            IList<string> lines = await FileIO.ReadLinesAsync(file);

            // Verify that it hasn't been done by someone else in the meantime.
            if (_names.Count == 0)
            {
                for (int i = 0; i < lines.Count; i += 3)
                {
                    _names.Add(new SkypeNames() { Name = lines[i] });
                }
            }
            _names = _names.OrderBy(n => n.Name).ToList();
        }

        /// <summary>
        /// Do a fuzzy search on all names and order results based on a pre-defined rule set
        /// </summary>
        /// <param name="query">The part of the name to look for</param>
        /// <returns>An ordered list of names that matches the query</returns>
        public static IEnumerable<SkypeNames> GetMatchingNames(string query)
        {
            return SkypeNameDataSource._names
                .Where(n => n.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) > -1)
                .OrderByDescending(n => n.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}

