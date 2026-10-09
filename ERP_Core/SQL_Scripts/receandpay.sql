CREATE PROCEDURE dbo.sp_GetDashboardPayablesAndReceivablesSummary
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL
    BEGIN
        SELECT @FromDate = CAST(ISNULL(MAX(BillDate), GETDATE()) AS DATE) FROM dbo.BillMasters WHERE (CompanyId = @CompanyId OR @CompanyId IS NULL OR CompanyId = 0);
    END
    IF @ToDate IS NULL
    BEGIN
        SELECT @ToDate = CAST(ISNULL(MAX(BillDate), GETDATE()) AS DATE) FROM dbo.BillMasters WHERE (CompanyId = @CompanyId OR @CompanyId IS NULL OR CompanyId = 0);
    END

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    ----------------------------------------------------------------------------
    -- 1. RESULT SET 1: CUSTOMER RECEIVABLES
    ----------------------------------------------------------------------------
    ;WITH Cust_OB_CTE AS (
        SELECT 
            obd.COAId,
            SUM(CASE 
                    WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'DEB' THEN ISNULL(obd.OBDtlOpenBlnc, 0)
                    WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'CRE' THEN -ISNULL(obd.OBDtlOpenBlnc, 0)
                    ELSE 0 
                END) AS VchrOpeningBal
        FROM dbo.OpeningBalanceDetails obd
        INNER JOIN dbo.OpeningBalances ob ON obd.OBId = ob.Id
        WHERE (ob.CompanyId = @CompanyId OR @CompanyId IS NULL OR ob.CompanyId = 0)
          AND (ob.Active IS NULL OR ob.Active = 1)
        GROUP BY obd.COAId
    ),
    Cust_PriorTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Debit, 0) - ISNULL(at.Credit, 0)) AS PriorTxBal
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate < @FromDateTime
        GROUP BY at.ChartofAccountId
    ),
    Cust_PeriodTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Debit, 0)) AS TotalInvoiced,
            SUM(ISNULL(at.Credit, 0)) AS TotalPaid,
            MAX(at.ExtraDate) AS LastTxDate
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate >= @FromDateTime
          AND at.ExtraDate <= @ToDateTime
        GROUP BY at.ChartofAccountId
    )
    SELECT 
        v.Id AS CustomerId,
        v.Name AS CustomerName,
        coa.AccountCode,
        ISNULL(v.City, '-') AS City,
        ISNULL(v.Phone, '-') AS Phone,
        CAST(ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0) AS DECIMAL(18,2)) AS OpeningBalance,
        CAST(ISNULL(per.TotalInvoiced, 0) AS DECIMAL(18,2)) AS TotalInvoiced,
        CAST(ISNULL(per.TotalPaid, 0) AS DECIMAL(18,2)) AS TotalPaid,
        CAST(
            (ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0))
            + ISNULL(per.TotalInvoiced, 0) 
            - ISNULL(per.TotalPaid, 0) 
        AS DECIMAL(18,2)) AS ReceivableBalance,
        CASE 
            WHEN ((ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0)) + ISNULL(per.TotalInvoiced, 0) - ISNULL(per.TotalPaid, 0)) > 0 THEN 'Due'
            WHEN ((ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0)) + ISNULL(per.TotalInvoiced, 0) - ISNULL(per.TotalPaid, 0)) < 0 THEN 'Advance'
            ELSE 'Clear'
        END AS Status,
        per.LastTxDate
    FROM dbo.Vendors v
    LEFT JOIN dbo.ChartOfAccounts coa ON v.ChartOfAccountId = coa.Id
    LEFT JOIN Cust_OB_CTE ob ON v.ChartOfAccountId = ob.COAId
    LEFT JOIN Cust_PriorTx_CTE pt ON v.ChartOfAccountId = pt.ChartofAccountId
    LEFT JOIN Cust_PeriodTx_CTE per ON v.ChartOfAccountId = per.ChartofAccountId
    WHERE (v.VendorType = 'Customer' OR v.VendorType = '2')
      AND (v.CompanyId = @CompanyId OR @CompanyId IS NULL OR v.CompanyId = 0)
      AND (v.Active IS NULL OR v.Active = 1)
    ORDER BY ReceivableBalance DESC, v.Name ASC;

    ----------------------------------------------------------------------------
    -- 2. RESULT SET 2: SUPPLIER PAYABLES
    ----------------------------------------------------------------------------
    ;WITH Supp_OB_CTE AS (
        SELECT 
            obd.COAId,
            SUM(CASE 
                    WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'CRE' THEN ISNULL(obd.OBDtlOpenBlnc, 0)
                    WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'DEB' THEN -ISNULL(obd.OBDtlOpenBlnc, 0)
                    ELSE 0 
                END) AS VchrOpeningBal
        FROM dbo.OpeningBalanceDetails obd
        INNER JOIN dbo.OpeningBalances ob ON obd.OBId = ob.Id
        WHERE (ob.CompanyId = @CompanyId OR @CompanyId IS NULL OR ob.CompanyId = 0)
          AND (ob.Active IS NULL OR ob.Active = 1)
        GROUP BY obd.COAId
    ),
    Supp_PriorTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Credit, 0) - ISNULL(at.Debit, 0)) AS PriorTxBal
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate < @FromDateTime
        GROUP BY at.ChartofAccountId
    ),
    Supp_PeriodTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Credit, 0)) AS TotalSold,
            SUM(ISNULL(at.Debit, 0)) AS TotalPaid,
            MAX(at.ExtraDate) AS LastTxDate
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate >= @FromDateTime
          AND at.ExtraDate <= @ToDateTime
        GROUP BY at.ChartofAccountId
    )
    SELECT 
        v.Id AS SupplierId,
        v.Name AS SupplierName,
        coa.AccountCode,
        ISNULL(v.City, '-') AS VillageOrJetty,
        ISNULL(v.Phone, '-') AS Phone,
        CAST(ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0) AS DECIMAL(18,2)) AS OpeningBalance,
        CAST(ISNULL(per.TotalSold, 0) AS DECIMAL(18,2)) AS TotalSold,
        CAST(ISNULL(per.TotalPaid, 0) AS DECIMAL(18,2)) AS TotalPaid,
        CAST(
            (ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0))
            + ISNULL(per.TotalSold, 0) 
            - ISNULL(per.TotalPaid, 0) 
        AS DECIMAL(18,2)) AS PayableBalance,
        CASE 
            WHEN ((ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0)) + ISNULL(per.TotalSold, 0) - ISNULL(per.TotalPaid, 0)) > 0 THEN 'Due'
            WHEN ((ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0)) + ISNULL(per.TotalSold, 0) - ISNULL(per.TotalPaid, 0)) < 0 THEN 'Advance'
            ELSE 'Clear'
        END AS Status,
        per.LastTxDate
    FROM dbo.Vendors v
    LEFT JOIN dbo.ChartOfAccounts coa ON v.ChartOfAccountId = coa.Id
    LEFT JOIN Supp_OB_CTE ob ON v.ChartOfAccountId = ob.COAId
    LEFT JOIN Supp_PriorTx_CTE pt ON v.ChartOfAccountId = pt.ChartofAccountId
    LEFT JOIN Supp_PeriodTx_CTE per ON v.ChartOfAccountId = per.ChartofAccountId
    WHERE (v.VendorType = 'Supplier' OR v.VendorType = '1')
      AND (v.CompanyId = @CompanyId OR @CompanyId IS NULL OR v.CompanyId = 0)
      AND (v.Active IS NULL OR v.Active = 1)
    ORDER BY PayableBalance DESC, v.Name ASC;

    ----------------------------------------------------------------------------
    -- 3. RESULT SET 3: ADVANCES (Type = 'Advance' Debit/Credit Net Balance by Party Account)
    ----------------------------------------------------------------------------
    ;WITH AdvTx AS (
        SELECT 
            at.AccountTransactionId,
            at.ChartofAccountId,
            at.VoucherNo,
            ISNULL(at.Debit, 0) AS Debit,
            ISNULL(at.Credit, 0) AS Credit,
            at.Amount,
            at.ExtraDate,
            at.ChartofAccountName,
            at.AccountName,
            at.PartyName,
            CASE 
                WHEN at.ExtraDate >= @FromDateTime AND at.ExtraDate <= @ToDateTime THEN 1 
                ELSE 0 
            END AS IsInPeriod
        FROM dbo.AccountTransaction at
        LEFT JOIN dbo.ChartOfAccounts coa ON coa.Id = at.ChartofAccountId
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'ADVANCE' 
               OR at.Narration LIKE '%[[]Advance]%' 
               OR at.Narration LIKE '%Advance%'
               OR at.ChartofAccountName LIKE '%Advance%'
               OR coa.AccountName LIKE '%Advance%')
          AND at.ExtraDate <= @ToDateTime
          -- Exclude Bank & Cash accounts so only real user/party accounts are included
          AND at.ChartofAccountId NOT IN (SELECT bm.ChartOfAccountId FROM dbo.BankMasters bm WHERE bm.ChartOfAccountId IS NOT NULL)
          AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
    ),
    AdvAgg AS (
        SELECT 
            COALESCE(NULLIF(LTRIM(RTRIM(a.AccountName)), ''), NULLIF(LTRIM(RTRIM(a.ChartofAccountName)), ''), NULLIF(LTRIM(RTRIM(a.PartyName)), ''), coa.AccountName, v.Name, 'Party Advance') AS CustomerName,
            MAX(a.ChartofAccountId) AS CustomerId,
            MAX(coa.AccountCode) AS AccountCode,
            ISNULL(MAX(v.City), '-') AS City,
            ISNULL(MAX(v.Phone), '-') AS Phone,
            SUM(a.Debit) AS TotalDebit,
            SUM(a.Credit) AS TotalCredit,
            SUM(a.Debit) - SUM(a.Credit) AS NetBalance,
            CASE 
                WHEN SUM(a.Debit) >= SUM(a.Credit) THEN SUM(a.Debit) - SUM(a.Credit)
                ELSE SUM(a.Credit) - SUM(a.Debit)
            END AS AdvanceAmount,
            COUNT(a.AccountTransactionId) AS VoucherCount,
            MAX(a.ExtraDate) AS LastTxDate,
            MAX(a.VoucherNo) AS LatestVoucherNo,
            MAX(a.IsInPeriod) AS IsInPeriod
        FROM AdvTx a
        LEFT JOIN dbo.ChartOfAccounts coa ON a.ChartofAccountId = coa.Id
        LEFT JOIN dbo.Vendors v ON a.ChartofAccountId = v.ChartOfAccountId
        GROUP BY 
            COALESCE(NULLIF(LTRIM(RTRIM(a.AccountName)), ''), NULLIF(LTRIM(RTRIM(a.ChartofAccountName)), ''), NULLIF(LTRIM(RTRIM(a.PartyName)), ''), coa.AccountName, v.Name, 'Party Advance')
    )
    SELECT 
        CustomerId,
        CustomerName,
        AccountCode,
        City,
        Phone,
        CAST(AdvanceAmount AS DECIMAL(18,2)) AS AdvanceAmount,
        VoucherCount,
        LastTxDate,
        LatestVoucherNo
    FROM AdvAgg
    WHERE AdvanceAmount > 0
    ORDER BY AdvanceAmount DESC, CustomerName ASC;

    ----------------------------------------------------------------------------
    -- 4. RESULT SET 4: COMMISSIONS (Type = 'Commission' or income COA)
    ----------------------------------------------------------------------------
    ;WITH PeriodCommissions AS (
        SELECT 
            at.AccountTransactionId,
            at.VoucherNo,
            ISNULL(NULLIF(LTRIM(RTRIM(at.InvoiceNo)), ''), at.VoucherNo) AS InvoiceNo,
            ISNULL(at.Narration, at.ChartofAccountName) AS Narration,
            at.ChartofAccountName,
            CAST(CASE 
                WHEN at.Credit > 0 THEN at.Credit 
                WHEN at.Debit > 0 THEN at.Debit 
                ELSE ISNULL(at.Amount, 0) 
            END AS DECIMAL(18,2)) AS CommissionAmount,
            at.ExtraDate,
            1 AS IsInPeriod
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'COMMISSION' 
               OR at.ChartofAccountName LIKE '%Commission%')
          AND (at.Credit > 0 OR at.Amount > 0)
          AND at.ExtraDate >= @FromDateTime
          AND at.ExtraDate <= @ToDateTime
    ),
    RecentCommissionsCTE AS (
        SELECT TOP 10
            at.AccountTransactionId,
            at.VoucherNo,
            ISNULL(NULLIF(LTRIM(RTRIM(at.InvoiceNo)), ''), at.VoucherNo) AS InvoiceNo,
            ISNULL(at.Narration, at.ChartofAccountName) AS Narration,
            at.ChartofAccountName,
            CAST(CASE 
                WHEN at.Credit > 0 THEN at.Credit 
                WHEN at.Debit > 0 THEN at.Debit 
                ELSE ISNULL(at.Amount, 0) 
            END AS DECIMAL(18,2)) AS CommissionAmount,
            at.ExtraDate,
            0 AS IsInPeriod
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'COMMISSION' 
               OR at.ChartofAccountName LIKE '%Commission%')
          AND (at.Credit > 0 OR at.Amount > 0)
        ORDER BY at.ExtraDate DESC, at.AccountTransactionId DESC
    )
    SELECT * FROM PeriodCommissions
    UNION ALL
    SELECT * FROM RecentCommissionsCTE
    ORDER BY IsInPeriod DESC, CommissionAmount DESC, ExtraDate DESC;

    ----------------------------------------------------------------------------
    -- 5. RESULT SET 5: WEEKLY COMMISSION TREND (7 Days ending on @TrendEndDate)
    ----------------------------------------------------------------------------
    DECLARE @TrendEndDate DATE = ISNULL(@ToDate, CAST(GETDATE() AS DATE));

    ;WITH Last7Days AS (
        SELECT DATEADD(DAY, -6, @TrendEndDate) AS DayDate, 1 AS DaySeq
        UNION ALL
        SELECT DATEADD(DAY, -5, @TrendEndDate), 2
        UNION ALL
        SELECT DATEADD(DAY, -4, @TrendEndDate), 3
        UNION ALL
        SELECT DATEADD(DAY, -3, @TrendEndDate), 4
        UNION ALL
        SELECT DATEADD(DAY, -2, @TrendEndDate), 5
        UNION ALL
        SELECT DATEADD(DAY, -1, @TrendEndDate), 6
        UNION ALL
        SELECT @TrendEndDate, 7
    )
    SELECT 
        d.DayDate,
        CASE 
            WHEN d.DayDate = CAST(GETDATE() AS DATE) THEN 'Today'
            ELSE LEFT(DATENAME(WEEKDAY, d.DayDate), 3)
        END AS DayName,
        ISNULL(SUM(CASE 
            WHEN at.Credit > 0 THEN at.Credit 
            WHEN at.Debit > 0 THEN at.Debit 
            ELSE ISNULL(at.Amount, 0) 
        END), 0) AS DayCommission,
        CASE WHEN d.DayDate = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsToday
    FROM Last7Days d
    LEFT JOIN dbo.AccountTransaction at 
        ON CAST(at.ExtraDate AS DATE) = d.DayDate
        AND (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
        AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'COMMISSION' OR at.ChartofAccountName LIKE '%Commission%')
        AND (at.Credit > 0 OR at.Amount > 0)
    GROUP BY d.DayDate, d.DaySeq
    ORDER BY d.DaySeq ASC;

    ----------------------------------------------------------------------------
    -- 6. RESULT SET 6: MONTHLY COMMISSION TREND (5 Buckets for Month)
    ----------------------------------------------------------------------------
    DECLARE @MonthStart DATE = DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 1);
    DECLARE @MonthEnd DATE = EOMONTH(@TrendEndDate);
    DECLARE @TodayDate DATE = CAST(GETDATE() AS DATE);
    DECLARE @TodayDay INT = DAY(@TodayDate);
    DECLARE @IsCurrentMonth BIT = CASE WHEN YEAR(@TodayDate) = YEAR(@TrendEndDate) AND MONTH(@TodayDate) = MONTH(@TrendEndDate) THEN 1 ELSE 0 END;

    ;WITH MonthBuckets AS (
        SELECT 1 AS BucketSeq, 'Week 1 (1-7)' AS BucketLabel, 
               @MonthStart AS StartDate, 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 7) AS EndDate,
               CASE WHEN @IsCurrentMonth = 1 AND @TodayDay BETWEEN 1 AND 7 THEN 1 ELSE 0 END AS IsCurrent
        UNION ALL
        SELECT 2, 'Week 2 (8-14)', 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 8), 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 14),
               CASE WHEN @IsCurrentMonth = 1 AND @TodayDay BETWEEN 8 AND 14 THEN 1 ELSE 0 END
        UNION ALL
        SELECT 3, 'Week 3 (15-21)', 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 15), 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 21),
               CASE WHEN @IsCurrentMonth = 1 AND @TodayDay BETWEEN 15 AND 21 THEN 1 ELSE 0 END
        UNION ALL
        SELECT 4, 'Week 4 (22-28)', 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 22), 
               DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 28),
               CASE WHEN @IsCurrentMonth = 1 AND @TodayDay BETWEEN 22 AND 28 THEN 1 ELSE 0 END
        UNION ALL
        SELECT 5, 'Current (29-' + CAST(DAY(@MonthEnd) AS VARCHAR(2)) + ')', 
               CASE WHEN DAY(@MonthEnd) >= 29 THEN DATEFROMPARTS(YEAR(@TrendEndDate), MONTH(@TrendEndDate), 29) ELSE @MonthEnd END, 
               @MonthEnd,
               CASE WHEN @IsCurrentMonth = 1 AND @TodayDay >= 29 THEN 1 ELSE 0 END
    )
    SELECT 
        b.BucketSeq,
        b.BucketLabel,
        ISNULL(SUM(CASE 
            WHEN at.Credit > 0 THEN at.Credit 
            WHEN at.Debit > 0 THEN at.Debit 
            ELSE ISNULL(at.Amount, 0) 
        END), 0) AS BucketCommission,
        b.IsCurrent
    FROM MonthBuckets b
    LEFT JOIN dbo.AccountTransaction at 
        ON CAST(at.ExtraDate AS DATE) >= b.StartDate 
        AND CAST(at.ExtraDate AS DATE) <= b.EndDate
        AND (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
        AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'COMMISSION' OR at.ChartofAccountName LIKE '%Commission%')
        AND (at.Credit > 0 OR at.Amount > 0)
    GROUP BY b.BucketSeq, b.BucketLabel, b.IsCurrent
    ORDER BY b.BucketSeq ASC;

    ----------------------------------------------------------------------------
    -- 7. RESULT SET 7: RECENT BILLS (from dbo.BillMasters)
    ----------------------------------------------------------------------------
    SELECT TOP 20
        b.Id,
        b.BillNo,
        b.BillDate,
        ISNULL(v.Name, 'Walk-in Supplier') AS SupplierName,
        ISNULL(lot.LotsCount, 0) AS LotsCount,
        CAST(ISNULL(b.TotalGrossAmount, 0) AS DECIMAL(18,2)) AS TotalSale,
        CAST(ISNULL(b.CommissionAmount, 0) AS DECIMAL(18,2)) AS Commission,
        CAST(ISNULL(b.NetPayable, 0) AS DECIMAL(18,2)) AS NetPayable,
        ISNULL(b.Status, 'Due') AS Status
    FROM dbo.BillMasters b
    LEFT JOIN dbo.Vendors v ON b.SupplierId = v.Id
    OUTER APPLY (
        SELECT COUNT(1) AS LotsCount
        FROM dbo.BillDetails bd
        WHERE bd.BillId = b.Id
          AND (bd.Active IS NULL OR bd.Active = 1)
    ) lot
    WHERE (b.CompanyId = @CompanyId OR @CompanyId IS NULL OR b.CompanyId = 0)
      AND (b.Active IS NULL OR b.Active = 1)
      AND (@FromDate IS NULL OR CAST(b.BillDate AS DATE) >= @FromDate)
      AND (@ToDate IS NULL OR CAST(b.BillDate AS DATE) <= @ToDate)
    ORDER BY b.BillDate DESC, b.Id DESC;

    ----------------------------------------------------------------------------
    -- 8. RESULT SET 8: BILL SUMMARY KPI (in @FromDate to @ToDate)
    ----------------------------------------------------------------------------
    SELECT 
        COUNT(1) AS PeriodBillsCount,
        ISNULL(SUM(b.TotalGrossAmount), 0) AS PeriodSaleAmount,
        ISNULL(SUM(b.CommissionAmount), 0) AS PeriodCommissionAmount
    FROM dbo.BillMasters b
    WHERE (b.CompanyId = @CompanyId OR @CompanyId IS NULL OR b.CompanyId = 0)
      AND (b.Active IS NULL OR b.Active = 1)
      AND CAST(b.BillDate AS DATE) >= @FromDate
      AND CAST(b.BillDate AS DATE) <= @ToDate;

    ----------------------------------------------------------------------------
    -- 9. RESULT SET 9: BANK & CASH ACCOUNTS LIQUIDITY (from dbo.BankMasters)
    ----------------------------------------------------------------------------
    ;WITH Bank_OB_CTE AS (
        SELECT 
            obd.COAId,
            SUM(CASE 
                    WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'DEB' THEN ISNULL(obd.OBDtlOpenBlnc, 0)
                    WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'CRE' THEN -ISNULL(obd.OBDtlOpenBlnc, 0)
                    ELSE 0 
                END) AS VchrOpeningBal
        FROM dbo.OpeningBalanceDetails obd
        INNER JOIN dbo.OpeningBalances ob ON obd.OBId = ob.Id
        WHERE (ob.CompanyId = @CompanyId OR ob.CompanyId IS NULL OR @CompanyId IS NULL OR @CompanyId = 0)
          AND (ob.Active IS NULL OR ob.Active = 1)
        GROUP BY obd.COAId
    ),
    Bank_PeriodTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Debit, 0)) AS PeriodInflow,
            SUM(ISNULL(at.Credit, 0)) AS PeriodOutflow
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR at.CompanyId IS NULL OR @CompanyId IS NULL OR @CompanyId = 0)
          AND at.ExtraDate >= @FromDateTime
          AND at.ExtraDate <= @ToDateTime
        GROUP BY at.ChartofAccountId
    ),
    Bank_AllTimeTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Debit, 0) - ISNULL(at.Credit, 0)) AS AllTimeNetTx
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR at.CompanyId IS NULL OR @CompanyId IS NULL OR @CompanyId = 0)
          AND at.ExtraDate <= @ToDateTime
        GROUP BY at.ChartofAccountId
    )
    SELECT 
        bm.Id AS BankId,
        bm.BankName,
        ISNULL(bm.AccountName, bm.BankName) AS AccountName,
        ISNULL(bm.AccountNo, '-') AS AccountNo,
        ISNULL(bm.IbanNo, '-') AS IbanNo,
        ISNULL(pbm.BankName, CASE WHEN ISNULL(bm.IsCash, 0) = 1 THEN 'Cash Counter' ELSE 'Main Branch' END) AS BranchOrLocation,
        CAST(ISNULL(bm.IsCash, 0) AS BIT) AS IsCash,
        CAST(ISNULL(ob.VchrOpeningBal, 0) + ISNULL(alltx.AllTimeNetTx, 0) AS DECIMAL(18,2)) AS CurrentBalance,
        CAST(ISNULL(per.PeriodInflow, 0) AS DECIMAL(18,2)) AS InflowAmount,
        CAST(ISNULL(per.PeriodOutflow, 0) AS DECIMAL(18,2)) AS OutflowAmount,
        bm.ChartOfAccountId,
        CASE WHEN bm.Active = 0 THEN 'Inactive' ELSE 'Active' END AS Status
    FROM dbo.BankMasters bm
    LEFT JOIN dbo.BankMasters pbm ON bm.ParentBankId = pbm.Id
    LEFT JOIN Bank_OB_CTE ob ON bm.ChartOfAccountId = ob.COAId
    LEFT JOIN Bank_PeriodTx_CTE per ON bm.ChartOfAccountId = per.ChartofAccountId
    LEFT JOIN Bank_AllTimeTx_CTE alltx ON bm.ChartOfAccountId = alltx.ChartofAccountId
    WHERE (bm.CompanyId = @CompanyId OR bm.CompanyId IS NULL OR @CompanyId IS NULL OR @CompanyId = 0)
      AND (bm.Active IS NULL OR bm.Active = 1)
    ORDER BY bm.IsCash ASC, bm.Id ASC;

    ----------------------------------------------------------------------------
    -- 10. RESULT SET 10: TOP MARTEY (Type = 'Martey' Debit/Credit Calculation by User Account)
    ----------------------------------------------------------------------------
    ;WITH MarteyTx AS (
        SELECT 
            at.AccountTransactionId,
            at.ChartofAccountId,
            at.VoucherNo,
            ISNULL(at.Debit, 0) AS Debit,
            ISNULL(at.Credit, 0) AS Credit,
            at.Amount,
            at.ExtraDate,
            at.ChartofAccountName,
            at.AccountName,
            at.PartyName,
            CASE 
                WHEN at.ExtraDate >= @FromDateTime AND at.ExtraDate <= @ToDateTime THEN 1 
                ELSE 0 
            END AS IsInPeriod
        FROM dbo.AccountTransaction at
        LEFT JOIN dbo.ChartOfAccounts coa ON coa.Id = at.ChartofAccountId
        WHERE (at.CompanyId = @CompanyId OR at.CompanyId IS NULL OR at.CompanyId = 0)
          AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'MARTEY' 
               OR (at.Narration LIKE '%Martey%' AND at.Type IS NULL))
          AND at.ExtraDate <= @ToDateTime
          -- Exclude Bank & Cash accounts so only real user/party accounts are included
          AND at.ChartofAccountId NOT IN (SELECT bm.ChartOfAccountId FROM dbo.BankMasters bm WHERE bm.ChartOfAccountId IS NOT NULL)
          AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
    ),
    MarteyAgg AS (
        SELECT 
            m.ChartofAccountId,
            COALESCE(v.Name, coa.AccountName, m.AccountName, m.ChartofAccountName, m.PartyName, 'Martey Account') AS AccountTitle,
            coa.AccountCode,
            ISNULL(v.City, '-') AS Location,
            ISNULL(v.VendorType, 'Account') AS AccountType,
            SUM(m.Debit) AS TotalDebit,
            SUM(m.Credit) AS TotalCredit,
            SUM(m.Credit) - SUM(m.Debit) AS NetBalance,
            CASE 
                WHEN SUM(m.Credit) >= SUM(m.Debit) THEN SUM(m.Credit) - SUM(m.Debit)
                ELSE SUM(m.Debit) - SUM(m.Credit)
            END AS MarteyBalance,
            CASE 
                WHEN SUM(m.Credit) >= SUM(m.Debit) THEN 'Credit'
                ELSE 'Debit'
            END AS BalanceType,
            COUNT(m.AccountTransactionId) AS VoucherCount,
            MAX(m.ExtraDate) AS LastTxDate,
            MAX(m.VoucherNo) AS LatestVoucherNo,
            MAX(m.IsInPeriod) AS IsInPeriod
        FROM MarteyTx m
        LEFT JOIN dbo.ChartOfAccounts coa ON m.ChartofAccountId = coa.Id
        LEFT JOIN dbo.Vendors v ON m.ChartofAccountId = v.ChartOfAccountId
        GROUP BY 
            m.ChartofAccountId, 
            v.Name, 
            coa.AccountName, 
            m.AccountName, 
            m.ChartofAccountName, 
            m.PartyName, 
            coa.AccountCode, 
            v.City, 
            v.VendorType
    )
    SELECT 
        ChartofAccountId,
        AccountTitle,
        AccountCode,
        Location,
        AccountType,
        CAST(TotalDebit AS DECIMAL(18,2)) AS TotalDebit,
        CAST(TotalCredit AS DECIMAL(18,2)) AS TotalCredit,
        CAST(NetBalance AS DECIMAL(18,2)) AS NetBalance,
        CAST(MarteyBalance AS DECIMAL(18,2)) AS MarteyBalance,
        BalanceType,
        VoucherCount,
        LastTxDate,
        LatestVoucherNo,
        IsInPeriod
    FROM MarteyAgg
    WHERE MarteyBalance > 0 OR TotalCredit > 0 OR TotalDebit > 0
    ORDER BY IsInPeriod DESC, MarteyBalance DESC, AccountTitle ASC;

    ----------------------------------------------------------------------------
    -- 11. RESULT SET 11: TOP LOANS (Type = 'Loan' Debit/Credit Calculation by User Account)
    ----------------------------------------------------------------------------
    ;WITH LoanTx AS (
        SELECT 
            at.AccountTransactionId,
            at.ChartofAccountId,
            at.VoucherNo,
            ISNULL(at.Debit, 0) AS Debit,
            ISNULL(at.Credit, 0) AS Credit,
            at.Amount,
            at.ExtraDate,
            at.ChartofAccountName,
            at.AccountName,
            at.PartyName,
            CASE 
                WHEN at.ExtraDate >= @FromDateTime AND at.ExtraDate <= @ToDateTime THEN 1 
                ELSE 0 
            END AS IsInPeriod
        FROM dbo.AccountTransaction at
        LEFT JOIN dbo.ChartOfAccounts coa ON coa.Id = at.ChartofAccountId
        WHERE (at.CompanyId = @CompanyId OR at.CompanyId IS NULL OR at.CompanyId = 0)
          AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'LOAN' 
               OR at.Narration LIKE '%[[]Loan]%' 
               OR at.Narration LIKE '%Loan%' 
               OR at.ChartofAccountName LIKE '%Loan%' 
               OR coa.AccountName LIKE '%Loan%')
          AND at.ExtraDate <= @ToDateTime
          -- Exclude Bank & Cash accounts so only real user/party accounts are included
          AND at.ChartofAccountId NOT IN (SELECT bm.ChartOfAccountId FROM dbo.BankMasters bm WHERE bm.ChartOfAccountId IS NOT NULL)
          AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
    ),
    LoanAgg AS (
        SELECT 
            l.ChartofAccountId,
            COALESCE(v.Name, coa.AccountName, l.AccountName, l.ChartofAccountName, l.PartyName, 'Loan Account') AS AccountTitle,
            coa.AccountCode,
            ISNULL(v.City, '-') AS Location,
            ISNULL(v.VendorType, 'Account') AS AccountType,
            SUM(l.Debit) AS TotalDebit,
            SUM(l.Credit) AS TotalCredit,
            SUM(l.Debit) - SUM(l.Credit) AS NetBalance,
            CASE 
                WHEN SUM(l.Debit) >= SUM(l.Credit) THEN SUM(l.Debit) - SUM(l.Credit)
                ELSE SUM(l.Credit) - SUM(l.Debit)
            END AS LoanBalance,
            CASE 
                WHEN SUM(l.Debit) >= SUM(l.Credit) THEN 'Debit'
                ELSE 'Credit'
            END AS BalanceType,
            COUNT(l.AccountTransactionId) AS VoucherCount,
            MAX(l.ExtraDate) AS LastTxDate,
            MAX(l.VoucherNo) AS LatestVoucherNo,
            MAX(l.IsInPeriod) AS IsInPeriod
        FROM LoanTx l
        LEFT JOIN dbo.ChartOfAccounts coa ON l.ChartofAccountId = coa.Id
        LEFT JOIN dbo.Vendors v ON l.ChartofAccountId = v.ChartOfAccountId
        GROUP BY 
            l.ChartofAccountId, 
            v.Name, 
            coa.AccountName, 
            l.AccountName, 
            l.ChartofAccountName, 
            l.PartyName, 
            coa.AccountCode, 
            v.City, 
            v.VendorType
    )
    SELECT 
        ChartofAccountId,
        AccountTitle,
        AccountCode,
        Location,
        AccountType,
        CAST(TotalDebit AS DECIMAL(18,2)) AS TotalDebit,
        CAST(TotalCredit AS DECIMAL(18,2)) AS TotalCredit,
        CAST(NetBalance AS DECIMAL(18,2)) AS NetBalance,
        CAST(LoanBalance AS DECIMAL(18,2)) AS LoanBalance,
        BalanceType,
        VoucherCount,
        LastTxDate,
        LatestVoucherNo,
        IsInPeriod
    FROM LoanAgg
    WHERE LoanBalance > 0
    ORDER BY IsInPeriod DESC, LoanBalance DESC, AccountTitle ASC;

    ----------------------------------------------------------------------------
    -- 12. RESULT SET 12: TOP OTHER (Type = 'Other' Debit/Credit Calculation by User Account)
    ----------------------------------------------------------------------------
    ;WITH OtherTx AS (
        SELECT 
            at.AccountTransactionId,
            at.ChartofAccountId,
            at.VoucherNo,
            ISNULL(at.Debit, 0) AS Debit,
            ISNULL(at.Credit, 0) AS Credit,
            at.Amount,
            at.ExtraDate,
            at.ChartofAccountName,
            at.AccountName,
            at.PartyName,
            CASE 
                WHEN at.ExtraDate >= @FromDateTime AND at.ExtraDate <= @ToDateTime THEN 1 
                ELSE 0 
            END AS IsInPeriod
        FROM dbo.AccountTransaction at
        LEFT JOIN dbo.ChartOfAccounts coa ON coa.Id = at.ChartofAccountId
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND (UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'OTHER' 
               OR at.Narration LIKE '%[[]Other]%' 
               OR at.Narration LIKE '%Other%' 
               OR at.ChartofAccountName LIKE '%Other%' 
               OR coa.AccountName LIKE '%Other%')
          AND at.ExtraDate <= @ToDateTime
          -- Exclude Bank & Cash accounts so only real user/party accounts are included
          AND at.ChartofAccountId NOT IN (SELECT bm.ChartOfAccountId FROM dbo.BankMasters bm WHERE bm.ChartOfAccountId IS NOT NULL)
          AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
          AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
    ),
    OtherAgg AS (
        SELECT 
            o.ChartofAccountId,
            COALESCE(v.Name, coa.AccountName, o.AccountName, o.ChartofAccountName, o.PartyName, 'Other Account') AS AccountTitle,
            coa.AccountCode,
            ISNULL(v.City, '-') AS Location,
            ISNULL(v.VendorType, 'Account') AS AccountType,
            SUM(o.Debit) AS TotalDebit,
            SUM(o.Credit) AS TotalCredit,
            SUM(o.Debit) - SUM(o.Credit) AS NetBalance,
            CASE 
                WHEN SUM(o.Debit) >= SUM(o.Credit) THEN SUM(o.Debit) - SUM(o.Credit)
                ELSE SUM(o.Credit) - SUM(o.Debit)
            END AS OtherBalance,
            CASE 
                WHEN SUM(o.Debit) >= SUM(o.Credit) THEN 'Debit'
                ELSE 'Credit'
            END AS BalanceType,
            COUNT(o.AccountTransactionId) AS VoucherCount,
            MAX(o.ExtraDate) AS LastTxDate,
            MAX(o.VoucherNo) AS LatestVoucherNo,
            MAX(o.IsInPeriod) AS IsInPeriod
        FROM OtherTx o
        LEFT JOIN dbo.ChartOfAccounts coa ON o.ChartofAccountId = coa.Id
        LEFT JOIN dbo.Vendors v ON o.ChartofAccountId = v.ChartOfAccountId
        GROUP BY 
            o.ChartofAccountId, 
            v.Name, 
            coa.AccountName, 
            o.AccountName, 
            o.ChartofAccountName, 
            o.PartyName, 
            coa.AccountCode, 
            v.City, 
            v.VendorType
    )
    SELECT 
        ChartofAccountId,
        AccountTitle,
        AccountCode,
        Location,
        AccountType,
        CAST(TotalDebit AS DECIMAL(18,2)) AS TotalDebit,
        CAST(TotalCredit AS DECIMAL(18,2)) AS TotalCredit,
        CAST(NetBalance AS DECIMAL(18,2)) AS NetBalance,
        CAST(OtherBalance AS DECIMAL(18,2)) AS OtherBalance,
        BalanceType,
        VoucherCount,
        LastTxDate,
        LatestVoucherNo,
        IsInPeriod
    FROM OtherAgg
    WHERE OtherBalance > 0
    ORDER BY IsInPeriod DESC, OtherBalance DESC, AccountTitle ASC;
END;
