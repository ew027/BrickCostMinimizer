using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace BrickCostMinimizer {
    /// <summary>
    /// Find the cheapest store combination for a given wanted list. Currently searches UK stores only.
    /// </summary>
    class CostMinimizer {
        private string _wantedListFilename;
        private int _maxSellers;
        private int _sellerMinItems;
        
        private List<WantedListItem> _items;
        private List<Seller> _sellers;

        private Dictionary<string, string> _ukStores;
        private Dictionary<string, string> _whitelistStores;
        private Dictionary<string, string> _blacklistStores;
        private Dictionary<string, string> _imageUrls;
        
        /// <summary>
        /// Finds the lowest cost combination for the items in the wanted list and exports the results to a set of HTML files in the location 
        /// specified in the application config file
        /// </summary>
        public void FindLowestCostCombination(string wantedListFilename, int maxSellers) {
            _wantedListFilename = wantedListFilename;
            _maxSellers = maxSellers;
            _sellerMinItems = Int32.Parse(ConfigurationManager.AppSettings["SellerMinimumItems"]);

            try {
                _items = ReadBrickstoreFile(_wantedListFilename);
            } catch (Exception ex) {
                Console.WriteLine("Unable to read Brickstore/Brickstock file: {0}", ex.Message);
                return;
            }

            // create a dictionary from this list for lookup purposes
            Dictionary<string, int> itemLookup = new Dictionary<string, int>();
            
            for (int i = 0; i < _items.Count; i++) {
                itemLookup.Add(_items[i].Id + "-" + _items[i].ColorId, i);
            }

            Console.WriteLine("{0} wanted items to look for", _items.Count);

            GetBricklinkData();

            PrepareSellerData();

            PerformSearch();
        }

        private void GetBricklinkData() {
            // if a proxy is specified in the config file, configure it for use with the scraper
            bool useProxy = (ConfigurationManager.AppSettings["UseProxy"] == "true");

            string proxyAddress = string.Empty;
            int proxyPort = 0;

            if (useProxy) {
                proxyAddress = ConfigurationManager.AppSettings["ProxyAddress"];
                proxyPort = Int32.Parse(ConfigurationManager.AppSettings["ProxyPort"]);
            }

            int cacheStoresDays = Int32.Parse(ConfigurationManager.AppSettings["CacheStoreData"]);
            int cacheItemsDays = Int32.Parse(ConfigurationManager.AppSettings["CacheItemData"]);

            BricklinkScraper scraper = new BricklinkScraper(_items, proxyAddress, proxyPort, ConfigurationManager.AppSettings["BricklinkData"], cacheStoresDays, cacheItemsDays);
            
            // replace the wanted list with one with availability data
            _items = scraper.ScrapePriceGuideData();

            _imageUrls = scraper.GetImageUrls();
            _ukStores = scraper.GetStoreList();

            if (ConfigurationManager.AppSettings["UseWhitelist"] == "true") {
                _whitelistStores = scraper.GetStoreWhiteList();
            } else {
                _whitelistStores = _ukStores;
            }

            if (ConfigurationManager.AppSettings["UseBlacklist"] == "true") {
                _blacklistStores = scraper.GetStoreBlackList();
            } else {
                _blacklistStores = new Dictionary<string, string>();
            }

        }

        // create a list of sellers and the items they hold based on the scraped data
        private void PrepareSellerData() {
            Console.WriteLine("Checking for items sold by UK sellers");

            foreach (var item in _items) {
                List<ItemForSale> ukLots = new List<ItemForSale>();

                int origCount = item.AvailableItems.Count;

                decimal runningTotal = 0;

                foreach (var lot in item.AvailableItems) {
                    if (_ukStores.ContainsKey(lot.SellerName) && _whitelistStores.ContainsKey(lot.SellerName) && !_blacklistStores.ContainsKey(lot.SellerName)) {
                        ukLots.Add(lot);
                        runningTotal += lot.Price;
                    }
                }

                int finalCount = ukLots.Count;

                item.AvailableItems = ukLots;

                if (finalCount > 0) {
                    item.AverageSellerPrice = runningTotal / finalCount;
                } else {
                    item.AverageSellerPrice = 0;
                }

                Console.WriteLine(item.Name + ": " + finalCount + " of " + origCount + " in UK");
            }

            _sellers = new List<Seller>();

            // loop through the stores and create a list of sellers
            foreach (var store in _ukStores.Keys) {
                Seller seller = new Seller();

                seller.AvailableItems = new List<ItemForSale>();
                seller.Storename = store;
                seller.Username = _ukStores[store];

                _sellers.Add(seller);
            }

            // create a dictionary from this list for lookup purposes
            Dictionary<string, int> storeLookup = new Dictionary<string, int>();
            for (int i = 0; i < _sellers.Count; i++) {
                storeLookup.Add(_sellers[i].Storename, i);
            }

            // now loop through all available items and assign to the relevant sellers
            foreach (var item in _items) {
                foreach (var lot in item.AvailableItems) {
                    _sellers[storeLookup[lot.SellerName]].AvailableItems.Add(lot);
                }
            }

            for (int i = _sellers.Count - 1; i >= 0; i--) {
                if (_sellers[i].AvailableItems.Count < _sellerMinItems) {
                    _sellers.RemoveAt(i);
                } else {
                    _sellers[i].CreateLookupTable(_items);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Total seller count: " + _sellers.Count);
            Console.WriteLine();
        }

        /// <summary>
        /// Search the list of sellers to find the cheapest combinations
        /// </summary>
        private void PerformSearch() {
            int maxResults = Int32.Parse(ConfigurationManager.AppSettings["MaxResults"]);
            int maxThreads = Int32.Parse(ConfigurationManager.AppSettings["MaxThreads"]);

            string outputRootDir = ConfigurationManager.AppSettings["ResultsFolder"];

            string resultOutputDir = "";

            string subfolderName = Path.GetFileNameWithoutExtension(_wantedListFilename);

            if (Directory.Exists(outputRootDir + subfolderName)) {
                int counter = 1;
                bool folderDoesNotExist = true;

                while (folderDoesNotExist) {
                    subfolderName = subfolderName.Split('~')[0] + "~" + counter;

                    if (!Directory.Exists(outputRootDir + subfolderName)) {
                        folderDoesNotExist = false;
                        Directory.CreateDirectory(outputRootDir + subfolderName);
                    }

                    counter++;
                }
            } else {
                Directory.CreateDirectory(outputRootDir + subfolderName);
            }

            resultOutputDir = outputRootDir + subfolderName + "\\";

            File.Copy(outputRootDir + "index.html", resultOutputDir + "index.html");
            File.Copy(outputRootDir + "blank.html", resultOutputDir + "blank.html");

            SellerSearch search = new SellerSearch(_sellers, maxResults, _maxSellers, _items, _imageUrls, resultOutputDir, maxThreads);

            search.Execute();
            search.Export();
        }

        private List<WantedListItem> ReadBrickstoreFile(string filename) {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            XmlNode inventoryNode = doc.SelectSingleNode("/BrickStoreXML/Inventory");

            if (inventoryNode == null) {
                inventoryNode = doc.SelectSingleNode("/BrickStockXML/Inventory");
            }

            XmlNodeList itemNodeList = inventoryNode.SelectNodes("Item");

            List<WantedListItem> items = new List<WantedListItem>();

            foreach (XmlNode node in itemNodeList) {
                WantedListItem item = new WantedListItem();
                item.Id = node.SelectSingleNode("ItemID").InnerText;
                item.TypeId = node.SelectSingleNode("ItemTypeID").InnerText;
                item.ColorId = node.SelectSingleNode("ColorID").InnerText;
                item.Name = node.SelectSingleNode("ItemName").InnerText;
                item.TypeName = node.SelectSingleNode("ItemTypeName").InnerText;
                item.ColorName = node.SelectSingleNode("ColorName").InnerText;
                item.CategoryId = node.SelectSingleNode("CategoryID").InnerText;
                item.CategoryName = node.SelectSingleNode("CategoryName").InnerText;
                item.Status = node.SelectSingleNode("Status").InnerText;
                item.Quantity = Int32.Parse(node.SelectSingleNode("Qty").InnerText);
                item.Price = Decimal.Parse(node.SelectSingleNode("Price").InnerText);
                item.Condition = node.SelectSingleNode("Condition").InnerText;

                items.Add(item);
            }

            return items;
        }
    }
}
