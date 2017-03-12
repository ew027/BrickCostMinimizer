using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BrickCostMinimizer {
    /// <summary>
    /// A valid combination of sellers who together have all the required items on the wanted list in the right condition and quantity.
    /// </summary>
    class ResultSet {
        public string Description { get; set; }
        public List<string> Sellers { get; set; }
        public Dictionary<string, int> CurrentStatus { get; set; }
        public Dictionary<string, int> Required { get; set; }
        public Dictionary<string, decimal> SellerTotals { get; set; }
        public Dictionary<string, decimal> SellerItemsTotal { get; set; }
        public Dictionary<string, decimal> SellerLotsTotal { get; set; }
        public List<ItemForSale> CurrentItems { get; set; }
        public decimal Price { get; set; }
        public int FileIndex { get; set; }
        public long Id { get; set; }
        
        private Dictionary<string, List<ItemForSale>> finalItemsBySeller;
        private Dictionary<string, List<ItemForSale>> availableItemsLookup;

        public ResultSet() { }

        public ResultSet(List<WantedListItem> items) {
            this.Sellers = new List<string>();
            this.CurrentStatus = new Dictionary<string, int>();
            this.Required = new Dictionary<string, int>();
            this.CurrentItems = new List<ItemForSale>();
            this.SellerTotals = new Dictionary<string, decimal>();
            this.SellerItemsTotal = new Dictionary<string, decimal>();
            this.SellerLotsTotal = new Dictionary<string, decimal>();

            availableItemsLookup = new Dictionary<string, List<ItemForSale>>();
            
            foreach (var item in items) {
                this.CurrentStatus.Add(item.GetId(), 0);
                this.Required.Add(item.GetId(), item.Quantity);
                availableItemsLookup.Add(item.GetId(), new List<ItemForSale>());
            }

            finalItemsBySeller = new Dictionary<string, List<ItemForSale>>();
        }

        public void Reset() {
            this.Sellers.Clear();
            this.CurrentItems.Clear();
            this.SellerTotals.Clear();
            this.SellerLotsTotal.Clear();
            this.SellerItemsTotal.Clear();
            
            foreach (var key in this.Required.Keys) {
                this.CurrentStatus[key] = 0;
            }

            foreach (var key in this.Required.Keys) {
                this.availableItemsLookup[key].Clear();
            }

            this.finalItemsBySeller.Clear();
            this.Price = 0;
        }

        public bool IsComplete() {
            bool allComplete = true;

            foreach (var id in this.CurrentStatus.Keys) {
                if (this.CurrentStatus[id] < this.Required[id]) {
                    allComplete = false;
                }
            }

            return allComplete;
        }

        public decimal CalculateCost() {
            foreach (var item in this.CurrentItems) {
                availableItemsLookup[item.GetId()].Add(item);
            }

            foreach (var seller in this.Sellers) {
                finalItemsBySeller.Add(seller, new List<ItemForSale>());
            }

            // now loop through and sort the sub-item lists by price
            foreach (var id in this.Required.Keys) {
                //string id = itm.GetId();

                if (availableItemsLookup[id].Count > 1) {
                    availableItemsLookup[id] = availableItemsLookup[id].OrderBy(o => o.Price).ToList();
                }

                // reset the currentstatus dictionary and use to keep track of how many we still need
                this.CurrentStatus[id] = 0;

                foreach (var item in availableItemsLookup[id]) {
                    if (this.CurrentStatus[id] < this.Required[id]) {
                        if (item.Quantity > (this.Required[id] - this.CurrentStatus[id])) {
                            item.QuantityRequired = (this.Required[id] - this.CurrentStatus[id]);
                            this.CurrentStatus[id] += (this.Required[id] - this.CurrentStatus[id]);
                            finalItemsBySeller[item.SellerName].Add(item.Copy());
                        } else {
                            item.QuantityRequired = item.Quantity;
                            this.CurrentStatus[id] += item.Quantity;
                            finalItemsBySeller[item.SellerName].Add(item.Copy());
                        }
                    }
                }
            }

            foreach (var seller in this.Sellers) {
                decimal sellerTotal = 0;
                int sellerItems = 0;
                int sellerLots = 0;

                foreach (var item in finalItemsBySeller[seller]) {
                    sellerTotal += (item.Price * item.QuantityRequired);
                    sellerItems += item.QuantityRequired;
                    sellerLots++;
                }
                this.SellerTotals[seller] = sellerTotal;
                this.SellerItemsTotal[seller] = sellerItems;
                this.SellerLotsTotal[seller] = sellerLots;
                this.Price += sellerTotal;
            }

            return this.Price;
        }

        public string CreateDisplayHtml(Dictionary<string, WantedListItem> items, Dictionary<string, string> imageUrls, Dictionary<string, Seller> sellers) {
            StringBuilder logger = new StringBuilder();

            logger.AppendLine("<html><head><style> table { border-collapse: collapse; } * {font-family: arial; font-size: 1em; } </style></head><body>");

            logger.AppendLine("<table border=\"1\" cellspacing=\"0\" cellpadding=\"3\"><tr><th>Seller</th><th>Lots</th><th>Items</th><th>Total</th></tr>");
            foreach (var seller in this.Sellers) {
                logger.AppendLine("<tr><td><a href=\"#" + sellers[seller].Username + "\">" + seller + "</a></td><td>" + this.SellerLotsTotal[seller] + "</td><td>" + this.SellerItemsTotal[seller] + "</td><td>&pound;" + this.SellerTotals[seller] + "</td></tr>");
            }
            logger.AppendLine("</table>");

            foreach (var seller in this.Sellers) {
                logger.AppendLine("<a name=\"" + sellers[seller].Username + "\"><h3><a href=\"http://www.bricklink.com/store.asp?p=" + sellers[seller].Username + "\" target=\"_bricklink\">" + seller + "</a></h3>");
                logger.AppendLine("<table border=\"1\" width=\"100%\" cellspacing=\"0\" cellpadding=\"3\"><tr><th>&nbsp;</th><th>Part</th><th>Quantity</th><th>Cat</th><th>Price</th></tr>");
                foreach (var item in finalItemsBySeller[seller]) {
                    logger.AppendLine("<tr><td>" + (imageUrls.ContainsKey(item.GetId()) ? "<img src=\"" + imageUrls[item.GetId()] + "\" />" : "&nbsp;") + "</td><td>" + item.PartId + " - " + items[item.GetId()].Name + " (" + items[item.GetId()].ColorName + ")</td><td align=\"center\">" + item.QuantityRequired + "</td><td align=\"center\">" + item.Category + "</td><td>&pound;" + Math.Round(item.Price, 2) + "</td></tr>");
                }
                logger.AppendLine("<tr><th colspan=\"5\" align=\"right\">&pound;" + Math.Round(this.SellerTotals[seller], 2) + " (" + this.SellerItemsTotal[seller] + " items in " + this.SellerLotsTotal[seller] + " lots)</th></tr></table>");
            }
            logger.AppendLine("</body></html>");

            return logger.ToString();
        }

        public void CreateFile(string outputDir, string filename, string results) {
            StreamWriter logger = File.CreateText(outputDir + filename);
            logger.Write(results);
            logger.Close();
        }

    }

    class DisplayResult {
        public decimal Price { get; set; }
        public long Id { get; set; }
        public IList<int> Combination { get; set; }

    }
}
