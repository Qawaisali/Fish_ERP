-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetIceLabourReport
-- Description: Retrieves Ice & Labour Expenses Report from BillMasters.
--              In Aarhat business:
--                LoadingExpense = Ice Expense (برف خرچ)
--                LabourExpense  = Labour Expense (مزدوری خرچ)
-- Filters:
--   1. @FromDate (Date)
--   2. @ToDate (Date)
--   3. @SupplierId (Boat / Farmer / Supplier)
--   4. @ExpenseType ('All', 'Ice', 'Labour')
-- ==========================================================================================

USE [ICT_ERP_CORE_2026];
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetIceLabourReport
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @SupplierId INT = NULL,
    @ExpenseType VARCHAR(20) = 'All'
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE);
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    -- Filter Bills
    SELECT 
        b.Id AS BillId,
        b.BillNo,
        b.BillDate,
        b.SupplierId,
        ISNULL(s.Name, 'General Boat / Supplier') AS SupplierName,
        ISNULL(s.City, '-') AS SupplierCity,
        b.CustomerId AS MasterCustomerId,
        ISNULL(c.Name, '') AS MasterCustomerName,
        ISNULL(b.TotalGrossAmount, 0) AS TotalGrossCatch,
        ISNULL(b.LoadingExpense, 0) AS IceExpense,      -- LoadingExpense = Ice
        ISNULL(b.LabourExpense, 0) AS LabourExpense,    -- LabourExpense  = Labour
        (ISNULL(b.LoadingExpense, 0) + ISNULL(b.LabourExpense, 0)) AS CombinedExpense,
        ISNULL(b.OtherExpense, 0) AS OtherExpense,
        ISNULL(b.TotalExpense, 0) AS TotalExpense,
        ISNULL(b.CommissionAmount, 0) AS CommissionAmount,
        ISNULL(b.NetPayable, 0) AS NetPayable,
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
      AND (@SupplierId IS NULL OR @SupplierId = 0 OR b.SupplierId = @SupplierId OR EXISTS (SELECT 1 FROM dbo.BillDetails bd WHERE bd.BillId = b.Id AND bd.SupplierId = @SupplierId))
      AND (
          (@ExpenseType = 'Ice' AND ISNULL(b.LoadingExpense, 0) > 0)
          OR (@ExpenseType = 'Labour' AND ISNULL(b.LabourExpense, 0) > 0)
          OR ((@ExpenseType IS NULL OR @ExpenseType = '' OR @ExpenseType = 'All') AND (ISNULL(b.LoadingExpense, 0) > 0 OR ISNULL(b.LabourExpense, 0) > 0))
      );

    -- Aggregate Lot Buyer Names
    SELECT 
        bd.BillId,
        COUNT(bd.Id) AS LotsCount,
        STRING_AGG(DISTINCT cust.Name, ', ') WITHIN GROUP (ORDER BY cust.Name) AS BuyerNames
    INTO #BillLotAggregates
    FROM dbo.BillDetails bd
    INNER JOIN #FilteredBills fb ON bd.BillId = fb.BillId
    LEFT JOIN dbo.Vendors cust ON bd.CustomerId = cust.Id
    GROUP BY bd.BillId;

    -- =========================================================================
    -- RESULT SET 1: Overall Summary Totals (KPIs)
    -- =========================================================================
    SELECT 
        COUNT(fb.BillId) AS TotalBillsCount,
        ISNULL(SUM(fb.TotalGrossCatch), 0) AS TotalGrossCatch,
        CASE WHEN @ExpenseType = 'Labour' THEN 0 ELSE ISNULL(SUM(fb.IceExpense), 0) END AS TotalIceExpense,
        CASE WHEN @ExpenseType = 'Ice' THEN 0 ELSE ISNULL(SUM(fb.LabourExpense), 0) END AS TotalLabourExpense,
        CASE 
            WHEN @ExpenseType = 'Ice' THEN ISNULL(SUM(fb.IceExpense), 0)
            WHEN @ExpenseType = 'Labour' THEN ISNULL(SUM(fb.LabourExpense), 0)
            ELSE ISNULL(SUM(fb.CombinedExpense), 0)
        END AS TotalCombinedExpense,
        ISNULL(SUM(fb.OtherExpense), 0) AS TotalOtherExpense,
        ISNULL(SUM(fb.TotalExpense), 0) AS TotalExpenses,
        ISNULL(SUM(fb.NetPayable), 0) AS TotalNetPayable
    FROM #FilteredBills fb;

    -- =========================================================================
    -- RESULT SET 2: Detailed Line Items
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
        END AS BuyerNames,
        ISNULL(la.LotsCount, 0) AS LotsCount,
        fb.TotalGrossCatch,
        CASE WHEN @ExpenseType = 'Labour' THEN 0 ELSE fb.IceExpense END AS IceExpense,
        CASE WHEN @ExpenseType = 'Ice' THEN 0 ELSE fb.LabourExpense END AS LabourExpense,
        CASE 
            WHEN @ExpenseType = 'Ice' THEN fb.IceExpense
            WHEN @ExpenseType = 'Labour' THEN fb.LabourExpense
            ELSE fb.CombinedExpense
        END AS CombinedExpense,
        fb.OtherExpense,
        fb.TotalExpense,
        fb.CommissionAmount,
        fb.NetPayable,
        fb.Status,
        fb.Remarks
    FROM #FilteredBills fb
    LEFT JOIN #BillLotAggregates la ON fb.BillId = la.BillId
    ORDER BY fb.BillDate DESC, fb.BillId DESC;

    DROP TABLE #FilteredBills;
    DROP TABLE #BillLotAggregates;
END;
GO
