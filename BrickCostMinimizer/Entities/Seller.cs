using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace BrickCostMinimizer {
    /// <summary>
    /// Represents a Bricklink seller
    /// </summary>
    class Seller {
        public string Storename { get; set; }
        public string Username { get; set; }
        public List<ItemForSale> AvailableItems { get; set; }
        public BitArray ItemAvailiabilityArray { get; set; }
        
        public Dictionary<string, List<ItemForSale>> AvailableItemsById { get; set; }

        public Seller() {
            this.AvailableItemsById = new Dictionary<string, List<ItemForSale>>();
        }

        public void CreateLookupTable(List<WantedListItem> items) {
            foreach (var item in AvailableItems) {
                if (this.AvailableItemsById.ContainsKey(item.GetId())) {
                    this.AvailableItemsById[item.GetId()].Add(item);
                } else {
                    this.AvailableItemsById.Add(item.GetId(), new List<ItemForSale>());
                    this.AvailableItemsById[item.GetId()].Add(item);
                }
            }
            
            this.ItemAvailiabilityArray = new BitArray(items.Count);

            for (int i = 0; i < items.Count; i++) {
                this.ItemAvailiabilityArray.Set(i, this.AvailableItemsById.ContainsKey(items[i].GetId()));
            }
        }
    }
}
