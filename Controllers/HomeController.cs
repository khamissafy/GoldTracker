using GoldTrackerWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GoldTrackerWeb.Controllers
{
    public class HomeController : Controller
    {
        // صفحة البداية: طلب رفع الملف والأسعار
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoginAndUpload(IFormFile file, decimal price21, decimal price24)
        {
            var sessionData = new UserSessionData
            {
                Price21 = price21,
                Price24 = price24,
                Transactions = new List<GoldTransaction>()
            };

            if (file != null && file.Length > 0)
            {
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    // قراءة الملف وتخطي الهيدر
                    var lines = new List<string>();
                    while (!reader.EndOfStream) lines.Add(reader.ReadLine());

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var parts = lines[i].Split(',');
                        if (parts.Length < 5 || string.IsNullOrWhiteSpace(parts[0])) continue;

                        try
                        {
                            var t = new GoldTransaction
                            {
                                Id = int.Parse(parts[0]),
                                GoldType = (int)decimal.Parse(parts[1]),
                                Grams = decimal.Parse(parts[2]),
                                PurchasePricePerGram = decimal.Parse(parts[3]),
                                TotalPurchasePrice = decimal.Parse(parts[4]),
                                PurchaseDate = string.IsNullOrWhiteSpace(parts[5]) ? (DateTime?)null : DateTime.Parse(parts[5]),
                                Status = parts.Length > 9 ? parts[9] : "Owned"
                            };

                            // تكملة باقي البيانات إن وجدت
                            if (parts.Length > 10 && !string.IsNullOrWhiteSpace(parts[10])) t.SellPricePerGram = decimal.Parse(parts[10]);
                            if (parts.Length > 11 && !string.IsNullOrWhiteSpace(parts[11])) t.TotalSellValue = decimal.Parse(parts[11]);
                            if (parts.Length > 12 && !string.IsNullOrWhiteSpace(parts[12])) t.SellDate = DateTime.Parse(parts[12]);
                            if (parts.Length > 13 && !string.IsNullOrWhiteSpace(parts[13])) t.ParentId = int.Parse(parts[13]);

                            sessionData.Transactions.Add(t);
                        }
                        catch { /* تجاهل الأسطر التالفة */ }
                    }
                }
            }

            // حفظ البيانات في الجلسة (Session)
            SaveSession(sessionData);
            return RedirectToAction("Dashboard");
        }

        public IActionResult Dashboard()
        {
            var data = GetSession();
            if (data == null) return RedirectToAction("Index");

            // 1. حساب الجرامات المتبقية (منطق الكونسول)
            var holdings = new Dictionary<int, decimal>();

            // تهيئة القاموس بكل العمليات المملوكة
            foreach (var t in data.Transactions.Where(x => x.Status == "Owned"))
            {
                holdings[t.Id] = t.Grams;
            }

            // خصم الكميات المباعة من العمليات الأصلية
            foreach (var t in data.Transactions.Where(x => x.Status == "Sold" && x.ParentId.HasValue))
            {
                if (holdings.ContainsKey(t.ParentId.Value))
                {
                    holdings[t.ParentId.Value] -= t.Grams;
                }
            }

            // 2. تصفير العدادات
            data.TotalOwnedGrams = 0;
            data.TotalInvestedHeld = 0;
            data.CurrentPortfolioValue = 0;
            data.TotalNetProfit = 0; // سيتم تجميعه لاحقاً

            // 3. تحديث بيانات كل صف للعرض
            foreach (var t in data.Transactions)
            {
                // إذا كان العنصر مملوكاً، نحسب القيم بناءً على ما تبقى منه
                if (t.Status == "Owned" && holdings.ContainsKey(t.Id))
                {
                    t.RemainingGrams = holdings[t.Id];

                    if (t.RemainingGrams > 0)
                    {
                        decimal currentPrice = (t.GoldType == 24) ? data.Price24 : data.Price21;

                        // التكلفة الفعلية للجرامات المتبقية فقط
                        t.EffectiveCost = t.RemainingGrams * t.PurchasePricePerGram;

                        // القيمة السوقية الحالية
                        t.CurrentValue = t.RemainingGrams * currentPrice;

                        // الربح/الخسارة غير المحققة
                        t.UnrealizedProfit = t.CurrentValue - t.EffectiveCost;

                        // تجميع الإجماليات
                        data.TotalOwnedGrams += t.RemainingGrams;
                        data.TotalInvestedHeld += t.EffectiveCost;
                        data.CurrentPortfolioValue += t.CurrentValue;
                        data.TotalNetProfit += t.UnrealizedProfit;
                    }
                    else
                    {
                        // إذا تم بيع كامل الكمية، نصفر القيم للعرض
                        t.RemainingGrams = 0;
                        t.EffectiveCost = 0;
                        t.CurrentValue = 0;
                        t.UnrealizedProfit = 0;
                    }
                }
            }

            // 4. حساب الأرباح المحققة (الكاش) من عمليات البيع
            decimal realizedProfit = 0;
            foreach (var s in data.Transactions.Where(x => x.Status == "Sold"))
            {
                decimal costOfSold = s.Grams * s.PurchasePricePerGram;
                decimal sellValue = s.TotalSellValue ?? 0;
                realizedProfit += sellValue - costOfSold;
            }

            data.RealizedProfit = realizedProfit;
            data.TotalNetProfit += realizedProfit; // صافي الربح = المحقق + غير المحقق

            return View(data);
        }

        [HttpPost]
        public IActionResult AddTransaction(int goldType, decimal grams, decimal pricePerGram, DateTime date)
        {
            var data = GetSession();
            if (data == null) return RedirectToAction("Index");

            int newId = data.Transactions.Any() ? data.Transactions.Max(x => x.Id) + 1 : 1;

            var t = new GoldTransaction
            {
                Id = newId,
                GoldType = goldType,
                Grams = grams,
                PurchasePricePerGram = pricePerGram,
                TotalPurchasePrice = grams * pricePerGram,
                PurchaseDate = date,
                Status = "Owned"
            };

            data.Transactions.Add(t);
            SaveSession(data);
            return RedirectToAction("Dashboard");
        }

        public IActionResult ExportCsv()
        {
            var data = GetSession();
            if (data == null) return RedirectToAction("Index");

            var builder = new StringBuilder();
            // ترويسة الملف (Header)
            builder.AppendLine("ID,Gold Type,Grams,Purchase Price,Total Purchase,Purchase Date,Current Price,Current Value,Difference,Status,Sell Price,Total Sell,Sell Date,ParentID");

            foreach (var t in data.Transactions)
            {
                string pDate = t.PurchaseDate?.ToString("yyyy-MM-dd") ?? "";
                string sDate = t.SellDate?.ToString("yyyy-MM-dd") ?? "";

                // --- حساب القيم المفقودة (Difference) أثناء التصدير ---
                decimal currentPrice = 0;
                decimal currentValue = 0;
                decimal difference = 0;

                if (t.Status == "Owned")
                {
                    // إذا كان مملوكاً: الفرق هو الربح غير المحقق بناءً على السعر الحالي
                    currentPrice = t.GoldType == 24 ? data.Price24 : data.Price21;
                    currentValue = t.Grams * currentPrice;
                    difference = currentValue - t.TotalPurchasePrice;
                }
                else
                {
                    // إذا كان مباعاً: الفرق هو الربح المحقق (سعر البيع - سعر الشراء)
                    difference = (t.TotalSellValue ?? 0) - (t.Grams * t.PurchasePricePerGram);
                }

                // كتابة السطر في ملف CSV
                builder.AppendLine($"{t.Id},{t.GoldType},{t.Grams},{t.PurchasePricePerGram},{t.TotalPurchasePrice},{pDate},{currentPrice},{currentValue},{difference},{t.Status},{t.SellPricePerGram},{t.TotalSellValue},{sDate},{t.ParentId}");
            }

            // إضافة سطر الإجماليات في النهاية (اختياري، ليطابق ملفك القديم)
            decimal totalPL = data.Transactions.Where(x => x.Status == "Owned").Sum(x => (x.Grams * (x.GoldType == 24 ? data.Price24 : data.Price21)) - x.TotalPurchasePrice)
                            + data.Transactions.Where(x => x.Status == "Sold").Sum(x => (x.TotalSellValue ?? 0) - (x.Grams * x.PurchasePricePerGram));

            builder.AppendLine($",,,,,,,,Total P/L: {totalPL},,,,,,");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "Gold_Portfolio.csv");
        }

        // دوال مساعدة للتعامل مع الـ Session
        private void SaveSession(UserSessionData data)
        {
            HttpContext.Session.SetString("GoldData", JsonConvert.SerializeObject(data));
        }

        private UserSessionData GetSession()
        {
            var str = HttpContext.Session.GetString("GoldData");
            return string.IsNullOrEmpty(str) ? null : JsonConvert.DeserializeObject<UserSessionData>(str);
        }

        [HttpPost]
        public IActionResult SellGold(int parentId, decimal grams, decimal pricePerGram, DateTime date)
        {
            var data = GetSession();
            if (data == null) return RedirectToAction("Index");

            // 1. العثور على القطعة الأصلية
            var parentTransaction = data.Transactions.FirstOrDefault(x => x.Id == parentId);
            if (parentTransaction == null) return RedirectToAction("Dashboard");

            // 2. التحقق من أن الكمية المتاحة تكفي للبيع (حساب سريع)
            var soldGramsSoFar = data.Transactions
                .Where(x => x.ParentId == parentId && x.Status == "Sold")
                .Sum(x => x.Grams);

            decimal remaining = parentTransaction.Grams - soldGramsSoFar;

            if (grams > remaining)
            {
                // خطأ: المحاولة لبيع كمية أكبر من المتاحة (يمكنك إضافة رسالة خطأ هنا)
                return RedirectToAction("Dashboard");
            }

            // 3. إنشاء سجل عملية البيع
            int newId = data.Transactions.Any() ? data.Transactions.Max(x => x.Id) + 1 : 1;

            var saleTransaction = new GoldTransaction
            {
                Id = newId,
                ParentId = parentId,
                GoldType = parentTransaction.GoldType,

                Grams = grams, // الوزن المباع
                PurchasePricePerGram = parentTransaction.PurchasePricePerGram, // نفس سعر الشراء الأصلي لحساب الربح
                TotalPurchasePrice = grams * parentTransaction.PurchasePricePerGram,

                Status = "Sold",

                SellPricePerGram = pricePerGram,
                TotalSellValue = grams * pricePerGram,
                SellDate = date,
                PurchaseDate = parentTransaction.PurchaseDate // نحتفظ بتاريخ الشراء الأصلي للمرجعية
            };

            data.Transactions.Add(saleTransaction);
            SaveSession(data);

            return RedirectToAction("Dashboard");
        }
    }
}