using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace BrickCostMinimizer {
    /// <summary>
    /// Scrape the wanted list information from Bricklink. Also manages the cached item and store data. Default behaviour is to sleep
    /// for 3 seconds between Bricklink requests.
    /// </summary>
    public class BricklinkScraper {
        private const int SleepTime = 3000;

        private List<WantedListItem> _wantedItems;
        private WebProxy _proxySettings;
        private bool _useProxy;
        private CookieContainer _cookieJar;
        private Dictionary<string, string> _ukStores;
        private Dictionary<string, string> _whitelistStores;
        private Dictionary<string, string> _blacklistStores;
        private string _filepath;
        private Dictionary<string, string> _imageUrls;

        private int _maxStoreListAge;
        private int _maxPartDataAge;

        public BricklinkScraper(List<WantedListItem> wantedItems, string proxyServer, int proxyPort, string filepath, int storeAge, int partAge) {
            this._wantedItems = wantedItems;
            this._filepath = filepath;

            if (!_filepath.EndsWith("\\")) {
                _filepath += "\\";
            }

            if (!string.IsNullOrEmpty(proxyServer)) {
                _proxySettings = new WebProxy(proxyServer, proxyPort);
                _useProxy = true;
            } else {
                _useProxy = false;
            }

            _maxStoreListAge = storeAge;
            _maxPartDataAge = partAge;

            _cookieJar = new CookieContainer();

            _cookieJar.Add(new Cookie("isCountryID", "UK", "/", "www.bricklink.com"));
            _cookieJar.Add(new Cookie("viewCurrencyID", "27", "/", "www.bricklink.com"));
            
            _ukStores = new Dictionary<string, string>();
            _imageUrls = new Dictionary<string, string>();

            this.GetUkStoreList();

            Console.WriteLine("Loading image URLs");
            this.GetLocalImageUrlData();
        }

        /// <summary>
        /// Scrape the price guide data and return the list of wanted items updated with availability data
        /// </summary>
        public List<WantedListItem> ScrapePriceGuideData() {
            foreach (var wantedItem in _wantedItems) {
                List<ItemForSale> itemsForSale = null;

                if (this.LocalPartsDataExists(wantedItem.Id, wantedItem.ColorId)) {
                    Console.WriteLine(wantedItem.Name + " being retrieved from local cache");
                    itemsForSale = this.GetLocalPartData(wantedItem.Id, wantedItem.ColorId);
                    Console.WriteLine(itemsForSale.Count + " available items found");
                } else {
                    Console.WriteLine("Sleeping...");
                    System.Threading.Thread.Sleep(SleepTime);

                    try {
                        Console.WriteLine(wantedItem.Name + " being retrieved from Bricklink...");
                        itemsForSale = new List<ItemForSale>();

                        HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create("http://www.bricklink.com/catalogPG.asp?P=" + wantedItem.Id + "&colorID=" + wantedItem.ColorId);
                        req.Proxy = _proxySettings;
                        req.CookieContainer = _cookieJar;

                        HttpWebResponse response = (HttpWebResponse)req.GetResponse();

                        StreamReader reader = new StreamReader(response.GetResponseStream());
                        string pgdata = reader.ReadToEnd();

                        reader.Close();
                        response.Close();

                        string[] pageparts = pgdata.Split(new string[] { "<B>Currently Available</B>" }, StringSplitOptions.None);

                        // extract the image url from the first part of the page
                        Regex imageUrlRegex = new Regex(@"SRC=\'(http\:\/\/img\.bricklink\.com\/.*?)\'");

                        Match matchImg = imageUrlRegex.Match(pageparts[0]);
                        if (matchImg.Success) {
                            string imageUrl = matchImg.Groups[1].ToString();
                            if (!_imageUrls.ContainsKey(wantedItem.Id + "-" + wantedItem.ColorId)) {
                                _imageUrls.Add(wantedItem.Id + "-" + wantedItem.ColorId, imageUrl);
                            }
                        }

                        string newdata = "";
                        string useddata = "";

                        if (pageparts.Length == 2) {
                            // either new or used is missing
                            // if used is missing then pageparts[1] will contain <TD WIDTH="25%" BGCOLOR="DDDDDD">&nbsp;</TD>
                            Regex emptyTestRegex = new Regex(@"<TD WIDTH=\""25%\"" BGCOLOR=\""DDDDDD\"">&nbsp;</TD>");

                            if (emptyTestRegex.IsMatch(pageparts[1])) {
                                newdata = pageparts[1];
                            } else {
                                useddata = pageparts[1];
                            }
                        } else {
                            newdata = pageparts[1];
                            useddata = pageparts[2];
                        }

                        Regex itemListingRegex = new Regex(@"<A HREF=\""\/store\.asp\?sID=(\d*)\&.*?<IMG SRC=\""\/images\/box16(.)\.png\"".*?TITLE=\""Store\: (.*?)\"" ALIGN=\""ABSMIDDLE\""\>.*?<\/TD><TD>(\d*)<\/TD><TD>.*?\&nbsp\;\D*([\d,]*)\.(\d+)");

                        if (!string.IsNullOrEmpty(newdata)) {
                            string[] newitems = newdata.Split(new string[] { "</TD></TR>" }, StringSplitOptions.None);

                            foreach (var item in newitems) {
                                Match match = itemListingRegex.Match(item);
                                if (match.Success) {
                                    ItemForSale saleItem = new ItemForSale();
                                    saleItem.SellerName = match.Groups[3].ToString().Replace("|", "");
                                    saleItem.Category = "N";
                                    saleItem.Quantity = Int32.Parse(match.Groups[4].ToString());
                                    saleItem.Price = Decimal.Parse(match.Groups[5].ToString() + "." + match.Groups[6].ToString());
                                    saleItem.PartId = wantedItem.Id;
                                    saleItem.ColorId = wantedItem.ColorId;

                                    itemsForSale.Add(saleItem);

                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(useddata)) {
                            string[] useditems = useddata.Split(new string[] { "</TD></TR>" }, StringSplitOptions.None);

                            foreach (var item in useditems) {
                                Match match = itemListingRegex.Match(item);
                                if (match.Success) {
                                    ItemForSale saleItem = new ItemForSale();
                                    saleItem.SellerName = match.Groups[3].ToString().Replace("|", "");
                                    saleItem.Category = "U";
                                    saleItem.Quantity = Int32.Parse(match.Groups[4].ToString());
                                    saleItem.Price = Decimal.Parse(match.Groups[5].ToString() + "." + match.Groups[6].ToString());
                                    saleItem.PartId = wantedItem.Id;
                                    saleItem.ColorId = wantedItem.ColorId;

                                    itemsForSale.Add(saleItem);
                                }
                            }
                        }

                        this.SavePartData(wantedItem.Id, wantedItem.ColorId, itemsForSale);
                        Console.WriteLine(itemsForSale.Count + " available items found and saved to cache");
                    } catch (Exception ex) {
                        Console.WriteLine("Failed: " + ex.Message);
                    }
                }

                wantedItem.AvailableItems = itemsForSale;

            }

            Console.WriteLine("Saving image URLs");
            this.SaveImageUrlData();

            return _wantedItems;
        }

        private void GetUkStoreList() {
            _whitelistStores = this.GetLocalStoreData("whitelist.txt");
            _blacklistStores = this.GetLocalStoreData("blacklist.txt");

            if (this.LocalStoreListExists()) {
                Console.WriteLine("Getting store list from cache");
                _ukStores = this.GetLocalStoreData("stores.txt");
            } else {
                Console.WriteLine("Getting store list from Bricklink");
                HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create("http://www.bricklink.com/browseStores.asp?countryID=UK&groupState=Y");

                if (_useProxy) {
                    req.Proxy = _proxySettings;
                }

                req.CookieContainer = _cookieJar;

                HttpWebResponse response = (HttpWebResponse)req.GetResponse();

                StreamReader reader = new StreamReader(response.GetResponseStream());

                string pagedata = reader.ReadToEnd();

                reader.Close();

                string[] pageparts = pagedata.Split(new string[] { "BrickLink Stores" }, StringSplitOptions.None);

                // first part is before the title, second is the page header, third part contains the store list - we want to cut out the NI & IoM stores

                string storelist = pageparts[2].Split(new string[] { "Northern Ireland" }, StringSplitOptions.None)[0];

                Regex storeLinkRegex = new Regex(@"<A HREF=\'store\.asp\?p=(.*)\'\>(.*)<\/A\>");

                if (!string.IsNullOrEmpty(storelist)) {
                    string[] storeLinks = storelist.Split(new string[] { "<BR>" }, StringSplitOptions.None);

                    foreach (var storeLink in storeLinks) {
                        Match match = storeLinkRegex.Match(storeLink);
                        if (match.Success) {
                            _ukStores.Add(match.Groups[2].ToString().Replace("|", ""), match.Groups[1].ToString());
                        }
                    }
                }

                Console.WriteLine("Saving store list to cache");
                this.SaveStoreData();
            }
        }

        private bool LocalStoreListExists() {
            if (File.Exists(_filepath + "stores.txt")) {
                FileInfo fileinfo = new FileInfo(_filepath + "stores.txt");

                DateTime lastWriteDate = fileinfo.LastWriteTime;

                if (lastWriteDate.AddDays(_maxStoreListAge) > DateTime.Now) {
                    return true;
                } else {
                    return false;
                }
            } else {
                return false;
            }
        }

        private bool LocalPartsDataExists(string partId, string colourId) {
            if (File.Exists(_filepath + partId + "-" + colourId + ".txt")) {
                FileInfo fileinfo = new FileInfo(_filepath + partId + "-" + colourId + ".txt");

                DateTime lastWriteDate = fileinfo.LastWriteTime;

                if (lastWriteDate.AddDays(_maxPartDataAge) > DateTime.Now) {
                    return true;
                } else {
                    return false;
                }
            } else {
                return false;
            }
        }

        private List<ItemForSale> GetLocalPartData(string partId, string colourId) {
            Stream inputStream;
            StreamReader reader;

            inputStream = new FileStream(_filepath + partId + "-" + colourId + ".txt", FileMode.Open, FileAccess.Read);
            reader = new StreamReader(inputStream);

            List<ItemForSale> items = new List<ItemForSale>();

            while (!reader.EndOfStream) {
                string[] partData = reader.ReadLine().Split('|');
                ItemForSale item = new ItemForSale();

                item.SellerName = partData[0];
                item.Quantity = Int32.Parse(partData[1]);
                item.Price = Decimal.Parse(partData[2]);
                item.Category = partData[3];
                item.PartId = partId;
                item.ColorId = colourId;

                items.Add(item);
            }
            
            reader.Close();
            inputStream.Close();
            
            return items;
        }

        private Dictionary<string, string> GetLocalStoreData(string filename) {
            Stream inputStream;
            StreamReader reader;

            Dictionary<string, string> storeData = new Dictionary<string, string>();

            string fullPath = Path.Combine(_filepath, filename);

            if (File.Exists(fullPath)) {
                inputStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                reader = new StreamReader(inputStream);

                while (!reader.EndOfStream) {
                    string[] partData = reader.ReadLine().Split('|');

                    storeData.Add(partData[0], partData[1]);
                }

                reader.Close();
                inputStream.Close();
            }

            return storeData;
        }

        private void SaveStoreData() {
            StreamWriter logger = File.CreateText(_filepath + "stores.txt");

            foreach (string storename in _ukStores.Keys) {
                logger.WriteLine(storename + "|" + _ukStores[storename]);
            }
            logger.Close();
        }

        private void SavePartData(string partId, string colourId, List<ItemForSale> itemsForSale) {
            StreamWriter logger = File.CreateText(_filepath + partId + "-" + colourId + ".txt");

            foreach (var item in itemsForSale) {
                logger.WriteLine(item.SellerName + "|" + item.Quantity + "|" + item.Price + "|" + item.Category);
            }
            logger.Close();
        }

        private void GetLocalImageUrlData() {
            Stream inputStream;
            StreamReader reader;

            if (File.Exists(_filepath + "images.txt")) {
                inputStream = new FileStream(_filepath + "images.txt", FileMode.Open, FileAccess.Read);
                reader = new StreamReader(inputStream);

                while (!reader.EndOfStream) {
                    string[] partData = reader.ReadLine().Split('|');
                    
                    _imageUrls.Add(partData[0], partData[1]);
                }
                
                reader.Close();
                inputStream.Close();
            }
        }

        private void SaveImageUrlData() {
            StreamWriter logger = File.CreateText(_filepath + "images.txt");

            foreach (string partname in _imageUrls.Keys) {
                logger.WriteLine(partname + "|" + _imageUrls[partname]);
            }
            logger.Close();
        }

        public List<WantedListItem> GetListWithAvailabilityData() {
            return _wantedItems;
        }

        public Dictionary<string, string> GetStoreList() {
            return _ukStores;
        }

        public Dictionary<string, string> GetStoreWhiteList() {
            return _whitelistStores;
        }

        public Dictionary<string, string> GetStoreBlackList() {
            return _blacklistStores;
        }

        public Dictionary<string, string> GetImageUrls() {
            return _imageUrls;
        }
    }
}
