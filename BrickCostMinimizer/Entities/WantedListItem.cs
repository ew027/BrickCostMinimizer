using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrickCostMinimizer {
    /// <summary>
    /// Represents a item on a wanted list
    /// </summary>
    public class WantedListItem {
        public string Id { get; set; }
        public string TypeId { get; set; }
        public string ColorId { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string ColorName { get; set; }
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Status { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Condition { get; set; }
        public decimal AverageSellerPrice { get; set; }

        public string GetId() {
            return Id + "-" + ColorId;
        }

        public List<ItemForSale> AvailableItems { get; set; }
    }
}
