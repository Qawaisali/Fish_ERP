-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetSalesReport
-- Description: Retrieves Aarhat Mandi Sales Report & Box 3 Settlement Data.
--              In commission agency business: Our Sales = Commission Earned.
--              Box 3 Breakdown:
--                1. Total Sale (Receivable debited to buyers)
--                2. Less: Commission (Our Sales / Revenue credited to COA 4001)
--                3. Less: Expenses (Labour + Ice credited/paid)
--                4. Less: Martey / Deposit (Deducted from supplier)
--                5. Net Payable to Supplier (Net liability to Boat/Fisherman)
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
    SELECT 
        bd.BillId,
        COUNT(bd.Id) AS LotsCount,
        ISNULL(SUM(bd.Crates), 0) AS TotalCrates,
        ISNULL(SUM(bd.WeightKg), 0) AS TotalWeightKg,
        STRING_AGG(DISTINCT cust.Name, ', ') WITHIN GROUP (ORDER BY cust.Name) AS BuyerNames
    INTO #BillLotAggregates
    FROM dbo.BillDetails bd
    INNER JOIN #FilteredBills fb ON bd.BillId = fb.BillId
    LEFT JOIN dbo.Vendors cust ON bd.CustomerId = cust.Id
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
    -- RESULT SET 1: Overall Summary Totals (Box 3 Settlement Metrics)
    -- =========================================================================
    SELECT 
        ISNULL(SUM(fb.GrossSaleAmount), 0) AS TotalGrossSale,
        ISNULL(SUM(fb.CommissionAmount), 0) AS TotalCommissionSales,  -- Aarhat's Actual Sales Revenue
        ISNULL(SUM(fb.LabourExpense), 0) AS TotalLabourExpense,
        ISNULL(SUM(fb.IceExpense), 0) AS TotalIceExpense,
        ISNULL(SUM(fb.TotalExpense), 0) AS TotalExpenses,
        ISNULL(SUM(fb.Deposit), 0) AS TotalDeposit,                  -- Martey / Deposit Deductions
        ISNULL(SUM(fb.NetPayableAmount), 0) AS TotalNetPayable,
        COUNT(fb.BillId) AS TotalBillsCount,
        ISNULL(SUM(la.LotsCount), 0) AS TotalLotsCount,
        ISNULL(SUM(la.TotalWeightKg), 0) AS TotalWeightKg,
        ISNULL(SUM(la.TotalCrates), 0) AS TotalCrates
    FROM #FilteredBills fb
    LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId;

    -- =========================================================================
    -- RESULT SET 2: Detailed Line Items (Bill / Settlement Grid)
    -- =========================================================================
    SELECT 
        fb.BillId,
        fb.BillNo,
        fb.BillDate,
        fb.SupplierId,
        fb.SupplierName,
        fb.SupplierCity,
        CASE 
            WHEN la.BuyerNames IS NOT NULL AND LEN(la.BuyerNames) > 0 THEN la.BuyerNames
            WHEN fb.MasterCustomerName IS NOT NULL AND LEN(fb.MasterCustomerName) > 0 THEN fb.MasterCustomerName
            ELSE 'General Buyer'
        END AS CustomerNames,
        ISNULL(la.LotsCount, 0) AS LotsCount,
        ISNULL(la.TotalCrates, 0) AS TotalCrates,
        ISNULL(la.TotalWeightKg, 0) AS TotalWeightKg,
        fb.GrossSaleAmount,      -- Box 3: Total Sale (Receivable)
        fb.CommissionPercent,
        fb.CommissionAmount,     -- Box 3: Less Commission (Our Sales)
        fb.LabourExpense,
        fb.IceExpense,
        fb.TotalExpense,         -- Box 3: Less Expenses (Labour + Ice)
        fb.Deposit,              -- Box 3: Less Martey / Deposit
        fb.NetPayableAmount,     -- Box 3: Net Payable to Supplier
        ISNULL(lv.JournalVoucherNo, '') AS VoucherNo,
        ISNULL(lv.ExpenseVoucherNo, '') AS ExpenseVoucherNo,
        fb.Status,
        fb.Remarks
    FROM #FilteredBills fb
    LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId
    LEFT JOIN #LinkedVouchers lv ON (fb.BillId = lv.InvoiceId OR fb.BillNo = lv.InvoiceNo)
    ORDER BY fb.BillDate DESC, fb.BillId DESC;

    -- Cleanup temp tables
    DROP TABLE #FilteredBills;
    DROP TABLE #BillLotAggregates;
    DROP TABLE #LinkedVouchers;
END
GO
