using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrickCostMinimizer {
    /// <summary>
    /// Represents an item for sale held by a seller
    /// </summary>
    public class ItemForSale {
        public string SellerName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public string PartId { get; set; }
        public string ColorId { get; set; }
        public string Notes { get; set; }
        public int QuantityRequired { get; set; }
                
        public string GetId() {
            return PartId + "-" + ColorId;
        }

        public ItemForSale Copy() {
            ItemForSale newItem = new ItemForSale();
            newItem.SellerName = this.SellerName;
            newItem.Quantity = this.Quantity;
            newItem.Price = this.Price;
            newItem.Category = this.Category;
            newItem.PartId = this.PartId;
            newItem.ColorId = this.ColorId;
            newItem.Notes = this.Notes;
            newItem.QuantityRequired = this.QuantityRequired;
            return newItem;
        }
    }
}
