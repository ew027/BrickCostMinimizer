using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;
using System.IO;
using System.Diagnostics;

using Combinatorics.Collections;

namespace BrickCostMinimizer {
    /// <summary>
    /// Search through the sellers to find the cheapest combinations. It uses a multi-threaded approach, using the main thread to identify
    /// potential combinations and a configurable number of threads to perform a detailed search on each potential combination
    /// </summary>
    class SellerSearch {
        private Dictionary<int, List<PotentialCombination>> _results;
        private object _resultLock;
        private object _costLock;


        private List<Seller> _sellers;
        private int _maxResults;
        private int _maxCombinations;
        private int _maxThreads;
        private List<WantedListItem> _items;
        private Dictionary<string, WantedListItem> _itemsLookup;
        private Dictionary<string, Seller> _sellerLookup;
        private Dictionary<string, string> _imageUrls;
        private string _outputDir;
        private BlockingQueue<PotentialCombination> _queue;
        private EventWaitHandle _waitHandle;

        // accessed by the consumer threads so must be locked prior to setting
        private decimal _maxCost;

        // accessed by consumer threads so marked as volatile
        private volatile int _resultsProcessed;
        private volatile int _consumerCompleteCount = 0;

        public int GetNumberProcessed() {
            return _resultsProcessed;
        }

        public SellerSearch(List<Seller> sellers, int maxResults, int maxCombinations, List<WantedListItem> items, Dictionary<string, string> imageUrls, string outputDir, int maxThreads) {
            _results = new Dictionary<int,List<PotentialCombination>>();
            _sellers = sellers;
            _maxCombinations = maxCombinations;
            _maxResults = maxResults;
            _items = items;
            _imageUrls = imageUrls;
            _outputDir = outputDir;
            _resultLock = new object();
            _costLock = new object();
            _queue = new BlockingQueue<PotentialCombination>(1000);
            _waitHandle = new AutoResetEvent(false);
            _resultsProcessed = 0;
            _maxThreads = maxThreads;
            
            // create a dictionary from item list for lookup purposes
            _itemsLookup = new Dictionary<string, WantedListItem>();
            for (int i = 0; i < items.Count; i++) {
                _itemsLookup.Add(items[i].Id + "-" + items[i].ColorId, items[i]);
            }

            // create a dictionary from seller list for lookup purposes
            _sellerLookup = new Dictionary<string, Seller>();
            for (int i = 0; i < sellers.Count; i++) {
                _sellerLookup.Add(sellers[i].Storename, sellers[i]);
            }
        }

        // allow results to be added to the results collection from multiple threads
        private void AddResult(int combination, PotentialCombination result) {
            lock (_resultLock) {
                _results[combination].Add(result);
            }
        }

        public void Execute() {
            int[] sellerIdx = new int[_sellers.Count];

            // create an array of the seller indices
            for (int i = 0; i < _sellers.Count; i++) {
                sellerIdx[i] = i;
            }

            // create the threads to process the potential combinations
            for (int i = 0; i < _maxThreads; i++) {
                new Thread(new ThreadStart(ConsumeJob)).Start();
            }
            
            _queue.Start();

            for (int i = 1; i <= _maxCombinations; i++) {
                _results.Add(i, new List<PotentialCombination>());

                Combinations<int> combinations = new Combinations<int>(sellerIdx, i);

                Console.WriteLine("Searching " + combinations.Count + " combinations of " + i + " sellers:");

                int j = 0;
                foreach (IList<int> c in combinations) {
                    // to quickly check whether it's even worth checking the detailed inventory for each seller, we construct
                    // a bitarray of all the items, setting the value to 1 if the seller has any of that item. We then OR together
                    // the arrays from each seller in this combination and call GetBitArrayCardinality which counts the number
                    // set to 1 without having to iterate through the whole array. We then only do a full check if it matches the item count
                    BitArray baseArray = new BitArray(_items.Count);

                    foreach (var k in c) {
                        Seller seller = _sellers[k];

                        baseArray.Or(seller.ItemAvailiabilityArray);
                    }

                    if (GetBitArrayCardinality(baseArray) == _items.Count) {
                        // item count matches so add to queue for full check
                        _queue.Enqueue(new PotentialCombination(c, j, i));
                    }

                    Console.Write("\r{0} combinations searched", j + 1);

                    j++;
                }

                Console.WriteLine();
                Console.WriteLine();
            }

            // shut the consumer queue off and wait for the last one to signal it's done.
            _queue.Stop();
            if (_queue.Count > 0) {
                // wait for the _waitHandle.Set() call in ConsumeJob which gets called by the last completing thread
                _waitHandle.WaitOne();
            }

            for (int i = 1; i <= _maxCombinations; i++) {
                Console.WriteLine("{0} sellers: {1} valid combinations found", i, _results[i].Count);
            }
        }

        private void CheckCombination(PotentialCombination pc, ResultSet rs) {
            rs.Id = pc.CombinationId;

            foreach (var k in pc.SellerIds) {
                Seller seller = _sellers[k];

                rs.Sellers.Add(seller.Storename);

                foreach (var lot in seller.AvailableItems) {
                    rs.CurrentStatus[lot.GetId()] += lot.Quantity;
                    rs.CurrentItems.Add(lot);
                }
            }

            if (rs.IsComplete()) {
                decimal cost = rs.CalculateCost();

                // if less than the max number of results, add it to the results. If more than max number, only add if cost is
                // less than current max cost
                if (_results[pc.SellerCount].Count < _maxResults) {
                    if (cost > _maxCost) {
                        lock (_costLock) {
                            _maxCost = cost;
                        }
                    }

                    pc.Price = cost;
                    pc.Id = rs.Id;
                    
                    AddResult(pc.SellerCount, pc);
                } else {
                    if (cost < _maxCost) {
                        pc.Price = cost;
                        pc.Id = rs.Id;

                        AddResult(pc.SellerCount, pc);
                    }
                }
            }
        }

        private DisplayResult CreateDisplayResult(ResultSet rs, decimal cost, IList<int> c) {
            DisplayResult dr = new DisplayResult();

            dr.Combination = c;

            dr.Price = cost;
            dr.Id = rs.Id;

            return dr;
        }

        private void ConsumeJob() {
            PotentialCombination pc;

            ResultSet rs = new ResultSet(_items);

            while ((pc = _queue.Dequeue()) != null) {
                rs.Reset();
                CheckCombination(pc, rs);
                _resultsProcessed++;
            }

            _consumerCompleteCount++;

            // are we the last consumer to finish?
            if (_consumerCompleteCount == _maxThreads) {
                _waitHandle.Set();
            }

        }
        
        public void Export() {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<html><head><style> table { border-collapse: collapse; } * {font-family: arial; font-size: 1em; } </style></head><body><h3>Results generated on " + DateTime.Now + "</h3><p><a href=\"wantedlist.html\" target=\"details\">Wanted list</a></p>");

            for (int i = 1; i <= _maxCombinations; i++) {
                sb.AppendLine("<p><b>" + i + " seller solutions:</b></p><ul>");

                if (_results[i].Count > 0) {

                    List<PotentialCombination> results = _results[i].OrderBy(o => o.Price).ToList();

                    // if more results than the limit, restrict to the limit
                    int limit = (results.Count > _maxResults) ? _maxResults : results.Count;

                    for (int j = 0; j < limit; j++) {
                        var pc = results[j];

                        ResultSet rs = new ResultSet(_items);

                        foreach (var k in pc.SellerIds) {
                            Seller seller = _sellers[k];

                            rs.Sellers.Add(seller.Storename);

                            foreach (var lot in seller.AvailableItems) {
                                rs.CurrentStatus[lot.GetId()] += lot.Quantity;
                                rs.CurrentItems.Add(lot);
                            }
                        }

                        rs.CalculateCost();
                        
                        sb.AppendLine("<li><a href=\"" + i + "-" + j + ".html\" target=\"details\">" + string.Join(",", rs.Sellers.ToArray()) + " (&pound;" + pc.Price + ")</a></li>");
                        
                        rs.CreateFile("", _outputDir + i + "-" + j + ".html", rs.CreateDisplayHtml(_itemsLookup, _imageUrls, _sellerLookup));
                    }
                } else {
                    sb.AppendLine("<li>No results</li>");
                }

                sb.AppendLine("</ul>");
            }

            sb.AppendLine("</body></html>");
            
            this.ExportWantedList();

            StreamWriter logger = File.CreateText(_outputDir + "resultlist.html");

            logger.Write(sb.ToString());

            logger.Close();
        }

        private void ExportWantedList() {
            StreamWriter logger = File.CreateText(_outputDir + "wantedlist.html");

            logger.Write("<html><head><style> table { border-collapse: collapse; } * {font-family: arial; font-size: 1em; } </style></head><body>");

            logger.Write("<table border=\"1\" width=\"100%\" cellspacing=\"0\" cellpadding=\"3\"><tr><th>&nbsp;</th><th>Part</th><th>Quantity Reqd</th><th>Max Price</th><th>Lots available</th></tr>");
            foreach (var id in _itemsLookup.Keys) {
                var item = _itemsLookup[id];
                logger.WriteLine("<tr><td><img src=\"" + (_imageUrls.ContainsKey(item.GetId()) ? _imageUrls[item.GetId()] : "") + "\" /></td><td>" + item.Id + " - " + _itemsLookup[item.GetId()].Name + " (" + _itemsLookup[item.GetId()].ColorName + ")</td><td align=\"center\">" + item.Quantity + "</td><td>&pound;" + Math.Round(item.Price, 2) + "</td><td align=\"center\">" + item.AvailableItems.Count + "</td></tr>");

            }
            logger.Write("</table></body></html>");

            logger.Close();
        }

        // this method based on http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel
        // from http://stackoverflow.com/questions/5063178/counting-bits-set-in-a-net-bitarray-class/14354311
        private Int32 GetBitArrayCardinality(BitArray bitArray) {

            Int32[] ints = new Int32[(bitArray.Count >> 5) + 1];

            bitArray.CopyTo(ints, 0);

            Int32 count = 0;

            // fix for not truncated bits in last integer that may have been set to true with SetAll()
            ints[ints.Length - 1] &= ~(-1 << (bitArray.Count % 32));

            for (Int32 i = 0; i < ints.Length; i++) {

                Int32 c = ints[i];

                unchecked {
                    c = c - ((c >> 1) & 0x55555555);
                    c = (c & 0x33333333) + ((c >> 2) & 0x33333333);
                    c = ((c + (c >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;
                }

                count += c;
            }

            return count;

        }
    }

    class PotentialCombination {
        public IList<int> SellerIds;
        public int CombinationId;
        public int SellerCount;

        public decimal Price { get; set; }
        public long Id { get; set; }

        public PotentialCombination(IList<int> ids, int combinationId, int sellerCount) {
            SellerIds = ids;
            CombinationId = combinationId;
            SellerCount = sellerCount;
        }
    }
}
