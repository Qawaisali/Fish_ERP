-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetSalesReport
-- Description: Retrieves Aarhat Mandi Sales Report & Box 3 Settlement Data.
--              In commission agency business: Our Sales = Commission Earned.
--              Box 3 Breakdown:
--                1. Total Sale (Receivable debited to buyers)
--                2. Less: Commission (Our Sales / Revenue credited to COA 4001)
--                3. Less: Expenses (Labour + Ice credited/paid)
--                4. Net Payable to Supplier (Net liability to Boat/Fisherman)
-- ==========================================================================================

IF OBJECT_ID('dbo.sp_GetSalesReport', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetSalesReport;
GO

CREATE PROCEDURE dbo.sp_GetSalesReport
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @SupplierId INT = NULL,
    @CustomerId INT = NULL,
    @FinancialYearId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Set default date range to today if null
    IF @FromDate IS NULL
        SET @FromDate = CAST(GETDATE() AS DATE);

    IF @ToDate IS NULL
        SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    -- Temporary table for filtered bills
    SELECT 
        b.Id AS BillId,
        b.BillNo,
        b.BillDate,
        b.SupplierId,
        ISNULL(s.Name, 'General Supplier / Boat') AS SupplierName,
        ISNULL(s.City, '-') AS SupplierCity,
        b.CustomerId AS MasterCustomerId,
        ISNULL(c.Name, '') AS MasterCustomerName,
        ISNULL(b.TotalGrossAmount, 0) AS GrossSaleAmount,
        ISNULL(b.CommissionPercent, 0) AS CommissionPercent,
        ISNULL(b.CommissionAmount, 0) AS CommissionAmount,
        ISNULL(b.LabourExpense, 0) AS LabourExpense,
        ISNULL(b.LoadingExpense, 0) AS IceExpense,
        ISNULL(b.OtherExpense, 0) AS OtherExpense,
        ISNULL(b.TotalExpense, 0) AS TotalExpense,
        ISNULL(b.Deposit, 0) AS Deposit,
        ISNULL(b.NetPayable, 0) AS NetPayableAmount,
        ISNULL(b.Status, 'Due') AS Status,
        ISNULL(b.Remarks, '') AS Remarks,
        b.CompanyId
    INTO #FilteredBills
    FROM dbo.BillMasters b
    LEFT JOIN dbo.Vendors s ON b.SupplierId = s.Id
    LEFT JOIN dbo.Vendors c ON b.CustomerId = c.Id
    WHERE (b.CompanyId = @CompanyId OR @CompanyId IS NULL OR b.CompanyId IS NULL)
      AND b.BillDate >= @FromDateTime 
      AND b.BillDate <= @ToDateTime
      AND (@SupplierId IS NULL OR b.SupplierId = @SupplierId OR EXISTS (SELECT 1 FROM dbo.BillDetails bd WHERE bd.BillId = b.Id AND bd.SupplierId = @SupplierId))
      AND (@CustomerId IS NULL OR b.CustomerId = @CustomerId OR EXISTS (SELECT 1 FROM dbo.BillDetails bd WHERE bd.BillId = b.Id AND bd.CustomerId = @CustomerId));

    -- Aggregate lot-level details per bill
    -- If @CustomerId is provided, aggregate specifically for that customer's purchased lots
    SELECT 
        bd.BillId,
        COUNT(bd.Id) AS LotsCount,
        ISNULL(SUM(bd.Crates), 0) AS TotalCrates,
        ISNULL(SUM(bd.WeightKg), 0) AS TotalWeightKg,
        ISNULL(SUM(bd.Amount), 0) AS CustomerAmount,
        CASE WHEN ISNULL(SUM(bd.WeightKg), 0) > 0 
             THEN ROUND(ISNULL(SUM(bd.Amount), 0) / SUM(bd.WeightKg), 2)
             ELSE 0 
        END AS AvgRate,
        STRING_AGG(DISTINCT ISNULL(cust.Name, 'General Buyer'), ', ') WITHIN GROUP (ORDER BY ISNULL(cust.Name, 'General Buyer')) AS BuyerNames,
        STRING_AGG(DISTINCT ISNULL(fish.Name, 'Fish'), ', ') WITHIN GROUP (ORDER BY ISNULL(fish.Name, 'Fish')) AS FishNames
    INTO #BillLotAggregates
    FROM dbo.BillDetails bd
    INNER JOIN #FilteredBills fb ON bd.BillId = fb.BillId
    LEFT JOIN dbo.Vendors cust ON bd.CustomerId = cust.Id
    LEFT JOIN dbo.Fishes fish ON bd.FishId = fish.Id
    WHERE (@CustomerId IS NULL OR bd.CustomerId = @CustomerId OR (fb.MasterCustomerId = @CustomerId AND bd.CustomerId IS NULL))
    GROUP BY bd.BillId;

    -- Find linked Vouchers for JV and CPV
    SELECT 
        v.InvoiceNo,
        v.InvoiceId,
        MAX(CASE WHEN v.VoucherType = 'JV' OR v.VoucherNo LIKE 'JV-%' THEN v.VoucherNo END) AS JournalVoucherNo,
        MAX(CASE WHEN v.VoucherType = 'CP' OR v.VoucherType = 'CPV' OR v.VoucherNo LIKE 'CPV-%' THEN v.VoucherNo END) AS ExpenseVoucherNo
    INTO #LinkedVouchers
    FROM dbo.Vouchers v
    WHERE EXISTS (SELECT 1 FROM #FilteredBills fb WHERE fb.BillId = v.InvoiceId OR fb.BillNo = v.InvoiceNo)
    GROUP BY v.InvoiceNo, v.InvoiceId;

    -- =========================================================================
    -- RESULT SET 1: Overall Summary Totals
    -- =========================================================================
    IF @CustomerId IS NOT NULL
    BEGIN
        -- Customer Mode: Gross Sale is customer's total purchased amount (Receivable)
        SELECT 
            ISNULL(SUM(la.CustomerAmount), 0) AS TotalGrossSale,
            0.00 AS TotalCommissionSales,
            0.00 AS TotalLabourExpense,
            0.00 AS TotalIceExpense,
            0.00 AS TotalExpenses,
            0.00 AS TotalDeposit,
            ISNULL(SUM(la.CustomerAmount), 0) AS TotalNetPayable,
            COUNT(fb.BillId) AS TotalBillsCount,
            ISNULL(SUM(la.LotsCount), 0) AS TotalLotsCount,
            ISNULL(SUM(la.TotalWeightKg), 0) AS TotalWeightKg,
            ISNULL(SUM(la.TotalCrates), 0) AS TotalCrates
        FROM #FilteredBills fb
        INNER JOIN #BillLotAggregates la ON fb.BillId = la.BillId;
    END
    ELSE
    BEGIN
        -- Supplier / Consolidated Mode: Box 3 Settlement Metrics
        SELECT 
            ISNULL(SUM(fb.GrossSaleAmount), 0) AS TotalGrossSale,
            ISNULL(SUM(fb.CommissionAmount), 0) AS TotalCommissionSales,
            ISNULL(SUM(fb.LabourExpense), 0) AS TotalLabourExpense,
            ISNULL(SUM(fb.IceExpense), 0) AS TotalIceExpense,
            ISNULL(SUM(fb.TotalExpense), 0) AS TotalExpenses,
            ISNULL(SUM(fb.Deposit), 0) AS TotalDeposit,
            ISNULL(SUM(fb.NetPayableAmount), 0) AS TotalNetPayable,
            COUNT(fb.BillId) AS TotalBillsCount,
            ISNULL(SUM(la.LotsCount), 0) AS TotalLotsCount,
            ISNULL(SUM(la.TotalWeightKg), 0) AS TotalWeightKg,
            ISNULL(SUM(la.TotalCrates), 0) AS TotalCrates
        FROM #FilteredBills fb
        LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId;
    END

    -- =========================================================================
    -- RESULT SET 2: Detailed Line Items (Bill / Order Grid)
    -- =========================================================================
    SELECT 
        fb.BillId,
        fb.BillNo,
        fb.BillDate,
        fb.SupplierId,
        fb.SupplierName,
        fb.SupplierCity,
        @CustomerId AS CustomerId,
        CASE 
            WHEN @CustomerId IS NOT NULL THEN (SELECT TOP 1 ISNULL(Name, 'Buyer') FROM dbo.Vendors WHERE Id = @CustomerId)
            WHEN la.BuyerNames IS NOT NULL AND LEN(la.BuyerNames) > 0 THEN la.BuyerNames
            WHEN fb.MasterCustomerName IS NOT NULL AND LEN(fb.MasterCustomerName) > 0 THEN fb.MasterCustomerName
            ELSE 'General Buyer'
        END AS CustomerNames,
        la.FishNames,
        ISNULL(la.LotsCount, 0) AS LotsCount,
        ISNULL(la.TotalCrates, 0) AS TotalCrates,
        ISNULL(la.TotalWeightKg, 0) AS TotalWeightKg,
        ISNULL(la.AvgRate, 0) AS RatePerKg,
        ISNULL(la.CustomerAmount, 0) AS CustomerAmount,
        CASE WHEN @CustomerId IS NOT NULL THEN ISNULL(la.CustomerAmount, 0) ELSE fb.GrossSaleAmount END AS GrossSaleAmount,
        CASE WHEN @CustomerId IS NOT NULL THEN 0.00 ELSE fb.CommissionPercent END AS CommissionPercent,
        CASE WHEN @CustomerId IS NOT NULL THEN 0.00 ELSE fb.CommissionAmount END AS CommissionAmount,
        CASE WHEN @CustomerId IS NOT NULL THEN 0.00 ELSE fb.LabourExpense END AS LabourExpense,
        CASE WHEN @CustomerId IS NOT NULL THEN 0.00 ELSE fb.IceExpense END AS IceExpense,
        CASE WHEN @CustomerId IS NOT NULL THEN 0.00 ELSE fb.TotalExpense END AS TotalExpense,
        CASE WHEN @CustomerId IS NOT NULL THEN 0.00 ELSE fb.Deposit END AS Deposit,
        CASE WHEN @CustomerId IS NOT NULL THEN ISNULL(la.CustomerAmount, 0) ELSE fb.NetPayableAmount END AS NetPayableAmount,
        ISNULL(lv.JournalVoucherNo, '') AS VoucherNo,
        ISNULL(lv.ExpenseVoucherNo, '') AS ExpenseVoucherNo,
        fb.Status,
        fb.Remarks
    FROM #FilteredBills fb
    LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId
    LEFT JOIN #LinkedVouchers lv ON (fb.BillId = lv.InvoiceId OR fb.BillNo = lv.InvoiceNo)
    WHERE (@CustomerId IS NULL OR la.LotsCount > 0)
    ORDER BY fb.BillDate DESC, fb.BillId DESC;

    -- Cleanup temp tables
    DROP TABLE #FilteredBills;
    DROP TABLE #BillLotAggregates;
    DROP TABLE #LinkedVouchers;
END
GO
