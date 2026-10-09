-- ==========================================================================================
-- SCRIPT: Update_OpeningBalance_In_Payable_And_Receivable_SPs.sql
-- DESCRIPTION: Updates Stored Procedures for Supplier Payables & Customer Receivables
--              to include Opening Balances entered via Opening Balance Vouchers
--              (tables: OpeningBalances & OpeningBalanceDetails).
--
-- TARGET PROCEDURES:
--   1. dbo.sp_GetSupplierPayablesSummary
--   2. dbo.sp_GetSupplierLedger
--   3. dbo.sp_GetCustomerReceivablesSummary
--   4. dbo.sp_GetCustomerLedger
-- ==========================================================================================

USE [ICT_ERP_CORE_2026];
GO

-- ==========================================================================================
-- 1. PROCEDURE: dbo.sp_GetSupplierPayablesSummary
-- ==========================================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GetSupplierPayablesSummary
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE);
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    -- 1. Opening Balance Vouchers from OpeningBalanceDetails
    ;WITH OB_CTE AS (
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
    -- 2. Transactions before @FromDateTime (Historical transactions prior to report window)
    PriorTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Credit, 0) - ISNULL(at.Debit, 0)) AS PriorTxBal
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate < @FromDateTime
        GROUP BY at.ChartofAccountId
    ),
    -- 3. Transactions within the report window (@FromDateTime to @ToDateTime)
    PeriodTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Credit, 0)) AS TotalCredit,
            SUM(ISNULL(at.Debit, 0)) AS TotalDebit
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
        CAST(ISNULL(per.TotalCredit, 0) AS DECIMAL(18,2)) AS TotalSold,
        CAST(ISNULL(per.TotalDebit, 0) AS DECIMAL(18,2)) AS TotalPaid,
        CAST(
            (ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0))
            + ISNULL(per.TotalCredit, 0) 
            - ISNULL(per.TotalDebit, 0) 
        AS DECIMAL(18,2)) AS PayableBalance,
        CASE 
            WHEN ((ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0)) + ISNULL(per.TotalCredit, 0) - ISNULL(per.TotalDebit, 0)) > 0 THEN 'Payable'
            WHEN ((ISNULL(v.OpeningBalance, 0) + ISNULL(ob.VchrOpeningBal, 0) + ISNULL(pt.PriorTxBal, 0)) + ISNULL(per.TotalCredit, 0) - ISNULL(per.TotalDebit, 0)) < 0 THEN 'Advance'
            ELSE 'Clear'
        END AS Status
    FROM dbo.Vendors v
    LEFT JOIN dbo.ChartOfAccounts coa ON v.ChartOfAccountId = coa.Id
    LEFT JOIN OB_CTE ob ON v.ChartOfAccountId = ob.COAId
    LEFT JOIN PriorTx_CTE pt ON v.ChartOfAccountId = pt.ChartofAccountId
    LEFT JOIN PeriodTx_CTE per ON v.ChartOfAccountId = per.ChartofAccountId
    WHERE (v.VendorType = 'Supplier' OR v.VendorType = '1')
      AND (v.CompanyId = @CompanyId OR @CompanyId IS NULL OR v.CompanyId = 0)
      AND (v.Active IS NULL OR v.Active = 1)
    ORDER BY PayableBalance DESC, v.Name ASC;
END;
GO

-- ==========================================================================================
-- 2. PROCEDURE: dbo.sp_GetSupplierLedger
-- ==========================================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetSupplierLedger]
    @CompanyId        INT,
    @FromDate         DATE = NULL,
    @ToDate           DATE = NULL,
    @SupplierId       INT,
    @FinancialYearId  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SupplierName         NVARCHAR(250) = N'';
    DECLARE @SupplierCOAId        INT           = NULL;
    DECLARE @LegacyOB             DECIMAL(18, 2) = 0.00;
    DECLARE @VchrOB               DECIMAL(18, 2) = 0.00;
    DECLARE @VendorInitialBalance DECIMAL(18, 2) = 0.00;
    DECLARE @CompanyName          NVARCHAR(250) = N'';
    DECLARE @CompanyAddress       NVARCHAR(500) = N'';
    DECLARE @Logo                 NVARCHAR(500) = N'';

    SELECT
        @SupplierName  = ISNULL(v.Name, N''),
        @SupplierCOAId = v.ChartOfAccountId,
        @LegacyOB      = ISNULL(v.OpeningBalance, 0.00)
    FROM dbo.Vendors v
    WHERE v.Id = @SupplierId
      AND (v.CompanyId = @CompanyId OR @CompanyId IS NULL);

    SELECT
        @CompanyName    = ISNULL(c.CompanyName, N''),
        @CompanyAddress = ISNULL(c.Address, N''),
        @Logo           = ISNULL(c.Logo, N'')
    FROM dbo.CCompanies c
    WHERE c.CompanyId = @CompanyId;

    IF @SupplierCOAId IS NOT NULL
    BEGIN
        SELECT @VchrOB = ISNULL(SUM(
            CASE 
                WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'DEB' THEN ISNULL(obd.OBDtlOpenBlnc, 0)
                WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'CRE' THEN -ISNULL(obd.OBDtlOpenBlnc, 0)
                ELSE 0
            END), 0)
        FROM dbo.OpeningBalanceDetails obd
        INNER JOIN dbo.OpeningBalances ob ON obd.OBId = ob.Id
        WHERE obd.COAId = @SupplierCOAId
          AND (ob.CompanyId = @CompanyId OR @CompanyId IS NULL OR ob.CompanyId = 0)
          AND ob.FinancialYearId = @FinancialYearId
          AND (ob.Active IS NULL OR ob.Active = 1);
    END;

    SET @VendorInitialBalance = @LegacyOB + @VchrOB;

    IF OBJECT_ID('tempdb..#SupplierTxns') IS NOT NULL DROP TABLE #SupplierTxns;

    CREATE TABLE #SupplierTxns (
        AccountTransactionId INT,
        VoucherId            INT,
        VoucherCode          VARCHAR(50),
        InvoiceNo            NVARCHAR(100),
        Narration            NVARCHAR(1000),
        TxnDate              DATE,
        ChequeNo             NVARCHAR(50),
        ChequeDate           DATETIME,
        DebitAmount          DECIMAL(18, 2),
        CreditAmount         DECIMAL(18, 2)
    );

    IF @SupplierCOAId IS NOT NULL
    BEGIN
        INSERT INTO #SupplierTxns (
            AccountTransactionId, VoucherId, VoucherCode, InvoiceNo,
            Narration, TxnDate, ChequeNo, ChequeDate, DebitAmount, CreditAmount
        )
        SELECT
            at.AccountTransactionId,
            v.VoucherId,
            COALESCE(v.VoucherNo, at.VoucherNo, N'') AS VoucherCode,
            at.InvoiceNo,
            at.Narration,
            CAST(ISNULL(at.ExtraDate, v.VoucherDate) AS DATE) AS TxnDate,
            at.ChequeNo,
            at.ChequeDate,
            ISNULL(at.Debit, 0.00)  AS DebitAmount,
            ISNULL(at.Credit, 0.00) AS CreditAmount
        FROM dbo.AccountTransaction at
        INNER JOIN dbo.Voucher v ON v.VoucherId = at.VoucherId
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL)
          AND at.ChartofAccountId = @SupplierCOAId
          AND ISNULL(v.DeleteStatus, 0) = 0
          AND (v.ApprovalStatus = 'Approved' OR v.ApprovalStatus IS NULL);
    END;

    DECLARE @PriorPeriodMovement DECIMAL(18, 2) = 0.00;
    IF @FromDate IS NOT NULL
    BEGIN
        SELECT @PriorPeriodMovement = ISNULL(SUM(DebitAmount - CreditAmount), 0.00)
        FROM #SupplierTxns
        WHERE TxnDate < @FromDate;
    END;

    DECLARE @TotalOpeningBalance DECIMAL(18, 2) = @VendorInitialBalance + @PriorPeriodMovement;

    IF OBJECT_ID('tempdb..#InRangeTxns') IS NOT NULL DROP TABLE #InRangeTxns;

    SELECT *
    INTO #InRangeTxns
    FROM #SupplierTxns
    WHERE (@FromDate IS NULL OR TxnDate >= @FromDate)
      AND (@ToDate   IS NULL OR TxnDate <= @ToDate);

    DECLARE @PeriodMovement DECIMAL(18, 2) = 0.00;
    SELECT @PeriodMovement = ISNULL(SUM(DebitAmount - CreditAmount), 0.00) FROM #InRangeTxns;

    DECLARE @OutstandingBalance DECIMAL(18, 2) = @TotalOpeningBalance + @PeriodMovement;

    IF EXISTS (SELECT 1 FROM #InRangeTxns)
    BEGIN
        SELECT
            @SupplierName                          AS [Name],
            @TotalOpeningBalance                   AS [OpeningBalance],
            ir.TxnDate                             AS [Date],
            ir.InvoiceNo                           AS [InvoiceNo],
            ir.VoucherCode                         AS [VoucherCode],
            ir.Narration                           AS [Remarks],
            ir.ChequeNo                    AS [ChqNo],
            ir.ChequeDate                          AS [ChqDate],
            NULLIF(ir.DebitAmount, 0)              AS [Debit],
            NULLIF(ir.CreditAmount, 0)             AS [Credit],
            @TotalOpeningBalance
                + SUM(ir.DebitAmount - ir.CreditAmount)
                    OVER (ORDER BY ir.TxnDate, ir.VoucherId, ir.AccountTransactionId
                          ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS [Balance],
            @OutstandingBalance                    AS [OutstandingBalance],
            @CompanyName                           AS [CompanyName],
            @CompanyAddress                        AS [CompanyAddress],
            @Logo                                  AS [Logo]
        FROM #InRangeTxns ir
        ORDER BY ir.TxnDate, ir.VoucherId, ir.AccountTransactionId;
    END
    ELSE
    BEGIN
        SELECT
            @SupplierName                          AS [Name],
            @TotalOpeningBalance                   AS [OpeningBalance],
            ISNULL(@FromDate, CAST(GETDATE() AS DATE)) AS [Date],
            NULL                                   AS [InvoiceNo],
            NULL                                   AS [VoucherCode],
            N'No transactions in selected period'  AS [Remarks],
            NULL                                   AS [ChqNo],
            NULL                                   AS [ChqDate],
            NULL                                   AS [Debit],
            NULL                                   AS [Credit],
            @TotalOpeningBalance                   AS [Balance],
            @OutstandingBalance                    AS [OutstandingBalance],
            @CompanyName                           AS [CompanyName],
            @CompanyAddress                        AS [CompanyAddress],
            @Logo                                  AS [Logo];
    END;

    IF OBJECT_ID('tempdb..#SupplierTxns') IS NOT NULL DROP TABLE #SupplierTxns;
    IF OBJECT_ID('tempdb..#InRangeTxns') IS NOT NULL DROP TABLE #InRangeTxns;
END;
GO

-- ==========================================================================================
-- 3. PROCEDURE: dbo.sp_GetCustomerReceivablesSummary
-- ==========================================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GetCustomerReceivablesSummary
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE);
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    -- 1. Opening Balance Vouchers from OpeningBalanceDetails (Debit is positive receivable, Credit is negative)
    ;WITH OB_CTE AS (
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
    -- 2. Transactions before @FromDateTime (Historical transactions prior to report window)
    PriorTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Debit, 0) - ISNULL(at.Credit, 0)) AS PriorTxBal
        FROM dbo.AccountTransaction at
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate < @FromDateTime
        GROUP BY at.ChartofAccountId
    ),
    -- 3. Transactions within the report window (@FromDateTime to @ToDateTime)
    PeriodTx_CTE AS (
        SELECT 
            at.ChartofAccountId,
            SUM(ISNULL(at.Debit, 0)) AS TotalInvoiced,
            SUM(ISNULL(at.Credit, 0)) AS TotalPaid
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
        END AS Status
    FROM dbo.Vendors v
    LEFT JOIN dbo.ChartOfAccounts coa ON v.ChartOfAccountId = coa.Id
    LEFT JOIN OB_CTE ob ON v.ChartOfAccountId = ob.COAId
    LEFT JOIN PriorTx_CTE pt ON v.ChartOfAccountId = pt.ChartofAccountId
    LEFT JOIN PeriodTx_CTE per ON v.ChartOfAccountId = per.ChartofAccountId
    WHERE (v.VendorType = 'Customer' OR v.VendorType = '2')
      AND (v.CompanyId = @CompanyId OR @CompanyId IS NULL OR v.CompanyId = 0)
      AND (v.Active IS NULL OR v.Active = 1)
    ORDER BY ReceivableBalance DESC, v.Name ASC;
END;
GO

-- ==========================================================================================
-- 4. PROCEDURE: dbo.sp_GetCustomerLedger
-- ==========================================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GetCustomerLedger
    @CompanyId        INT = 1,
    @CustomerId        INT,
    @FromDate          DATE = NULL,
    @ToDate            DATE = NULL,
    @FinancialYearId   INT              -- NEW
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE);
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    DECLARE @COAId INT;
    DECLARE @VendorName NVARCHAR(200);
    DECLARE @LegacyOB DECIMAL(18,2) = 0;

    SELECT 
        @COAId = v.ChartOfAccountId,
        @VendorName = v.Name,
        @LegacyOB = ISNULL(v.OpeningBalance, 0)
    FROM dbo.Vendors v
    WHERE v.Id = @CustomerId;

    DECLARE @CompanyName NVARCHAR(200) = '';
    DECLARE @CompanyAddress NVARCHAR(500) = '';
    DECLARE @Logo NVARCHAR(500) = '';

    SELECT TOP 1
        @CompanyName = ISNULL(c.CompanyName, ''),
        @CompanyAddress = ISNULL(c.Address, ''),
        @Logo = ISNULL(c.Logo, '')
    FROM dbo.CCompanies c
    WHERE c.CompanyId = @CompanyId;

    DECLARE @VchrOB DECIMAL(18,2) = 0;
    IF @COAId IS NOT NULL
    BEGIN
        SELECT @VchrOB = ISNULL(SUM(
            CASE 
                WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'DEB' THEN ISNULL(obd.OBDtlOpenBlnc, 0)
                WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'CRE' THEN -ISNULL(obd.OBDtlOpenBlnc, 0)
                ELSE 0 
            END), 0)
        FROM dbo.OpeningBalanceDetails obd
        INNER JOIN dbo.OpeningBalances ob ON obd.OBId = ob.Id
        WHERE obd.COAId = @COAId
          AND (ob.CompanyId = @CompanyId OR @CompanyId IS NULL OR ob.CompanyId = 0)
          AND ob.FinancialYearId = @FinancialYearId       -- NEW
          AND (ob.Active IS NULL OR ob.Active = 1);
    END

    DECLARE @PriorTxBal DECIMAL(18,2) = 0;
    IF @COAId IS NOT NULL
    BEGIN
        SELECT @PriorTxBal = ISNULL(SUM(ISNULL(at.Debit, 0) - ISNULL(at.Credit, 0)), 0)
        FROM dbo.AccountTransaction at
        WHERE at.ChartofAccountId = @COAId
          AND (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate < @FromDateTime;
    END

    DECLARE @TotalOpeningBal DECIMAL(18,2) = @LegacyOB + @VchrOB + @PriorTxBal;

    ;WITH TxList AS (
        SELECT 
            at.AccountTransactionId,
            at.ExtraDate AS [Date],
            at.VoucherNo AS VoucherCode,
            at.InvoiceNo,
            ISNULL(at.Narration, ISNULL(at.EntryTypeName, 'Transaction')) AS Remarks,
            at.ChequeNo AS ChqNo,
            at.ChequeDate AS ChqDate,
            ISNULL(at.Debit, 0) AS Debit,
            ISNULL(at.Credit, 0) AS Credit,
            ROW_NUMBER() OVER (ORDER BY at.ExtraDate ASC, at.AccountTransactionId ASC) AS RowNum
        FROM dbo.AccountTransaction at
        WHERE at.ChartofAccountId = @COAId
          AND (at.CompanyId = @CompanyId OR @CompanyId IS NULL OR at.CompanyId = 0)
          AND at.ExtraDate >= @FromDateTime
          AND at.ExtraDate <= @ToDateTime
    ),
    RunningBal AS (
        SELECT 
            t.AccountTransactionId,
            t.[Date],
            t.VoucherCode,
            t.InvoiceNo,
            t.Remarks,
            t.ChqNo,
            t.ChqDate,
            t.Debit,
            t.Credit,
            CAST(@TotalOpeningBal + SUM(t.Debit - t.Credit) OVER (ORDER BY t.RowNum ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS DECIMAL(18,2)) AS Balance
        FROM TxList t
    )
    SELECT 
        @VendorName AS [Name],
        @TotalOpeningBal AS OpeningBalance,
        ISNULL((SELECT TOP 1 Balance FROM RunningBal ORDER BY AccountTransactionId DESC), @TotalOpeningBal) AS OutstandingBalance,
        @CompanyName AS CompanyName,
        @CompanyAddress AS CompanyAddress,
        @Logo AS Logo,
        rb.[Date],
        rb.VoucherCode,
        rb.InvoiceNo,
        rb.Remarks,
        rb.ChqNo,
        rb.ChqDate,
        rb.Debit,
        rb.Credit,
        rb.Balance
    FROM RunningBal rb
    ORDER BY rb.[Date] ASC, rb.AccountTransactionId ASC;

    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 
            @VendorName AS [Name],
            @TotalOpeningBal AS OpeningBalance,
            @TotalOpeningBal AS OutstandingBalance,
            @CompanyName AS CompanyName,
            @CompanyAddress AS CompanyAddress,
            @Logo AS Logo,
            @FromDateTime AS [Date],
            NULL AS VoucherCode,
            NULL AS InvoiceNo,
            'No transactions in selected period' AS Remarks,
            NULL AS ChqNo,
            NULL AS ChqDate,
            CAST(0 AS DECIMAL(18,2)) AS Debit,
            CAST(0 AS DECIMAL(18,2)) AS Credit,
            @TotalOpeningBal AS Balance;
    END
END;
GO
