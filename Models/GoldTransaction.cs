using System;
using System.Collections.Generic;

namespace GoldTrackerWeb.Models
{
    public class GoldTransaction
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public int GoldType { get; set; }
        public decimal Grams { get; set; }
        public decimal PurchasePricePerGram { get; set; }
        public decimal TotalPurchasePrice { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public string Status { get; set; }

        // تفاصيل البيع
        public decimal? SellPricePerGram { get; set; }
        public decimal? TotalSellValue { get; set; }
        public DateTime? SellDate { get; set; }

        // --- خصائص للعرض والحسابات (Runtime) ---
        public decimal RemainingGrams { get; set; } // الوزن المتبقي بعد البيع
        public decimal EffectiveCost { get; set; }  // تكلفة الوزن المتبقي فقط
        public decimal CurrentValue { get; set; }   // قيمته الحالية في السوق
        public decimal UnrealizedProfit { get; set; } // الربح غير المحقق (ورقي)
    }

    public class UserSessionData
    {
        public decimal Price24 { get; set; }
        public decimal Price21 { get; set; }
        public List<GoldTransaction> Transactions { get; set; } = new List<GoldTransaction>();

        // ملخصات للمحفظة
        public decimal TotalOwnedGrams { get; set; }
        public decimal TotalInvestedHeld { get; set; }
        public decimal CurrentPortfolioValue { get; set; }
        public decimal RealizedProfit { get; set; }
        public decimal TotalNetProfit { get; set; }
    }
}