-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetAccountLedger
-- Description: Retrieves Account Ledger transactions and calculates Running Balance 
--              for any transable Chart of Account (COA).
--
-- Balance Calculation:
--   1. Voucher Opening Balance from OpeningBalanceDetails (for selected FinancialYear)
--   2. Prior Transactions Balance (AccountTransaction before @FromDate)
--   3. Total Opening Balance = Voucher OB + Prior Transactions Balance
--   4. Running Balance in-period: Opening Balance + Cumulative (Debit - Credit)
-- ==========================================================================================

IF OBJECT_ID('dbo.sp_GetAccountLedger', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetAccountLedger;
GO

CREATE PROCEDURE dbo.sp_GetAccountLedger
    @CompanyId INT = 1,
    @ChartOfAccountId INT,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @FinancialYearId INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Date Range defaults
    IF @FromDate IS NULL
        SET @FromDate = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    IF @ToDate IS NULL
        SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    -- 2. Fetch Account Details
    DECLARE @AccountCode VARCHAR(50);
    DECLARE @AccountName NVARCHAR(200);
    DECLARE @NatureName NVARCHAR(100) = '';
    DECLARE @CompanyName NVARCHAR(200) = '';

    SELECT 
        @AccountCode = coa.AccountCode,
        @AccountName = coa.AccountName,
        @NatureName = ISNULL(noa.AccountName, '')
    FROM dbo.ChartOfAccounts coa
    LEFT JOIN dbo.NatureofAccount noa ON noa.Id = coa.NatureOfAccountId
    WHERE coa.Id = @ChartOfAccountId;

    -- Fetch Company Name
    SELECT TOP 1 @CompanyName = CompanyName 
    FROM dbo.CCompanies 
    WHERE CompanyId = @CompanyId;

    -- 3. Calculate Voucher Opening Balance from OpeningBalanceDetails
    DECLARE @VoucherOpeningBal DECIMAL(18, 4) = 0;

    SELECT @VoucherOpeningBal = ISNULL(SUM(
        CASE 
            WHEN UPPER(LTRIM(RTRIM(ISNULL(obd.OBDtlTranType, '')))) IN ('DEB', 'DR', 'DEBIT') 
                THEN ISNULL(obd.OBDtlOpenBlnc, 0)
            WHEN UPPER(LTRIM(RTRIM(ISNULL(obd.OBDtlTranType, '')))) IN ('CRE', 'CR', 'CREDIT') 
                THEN -ISNULL(obd.OBDtlOpenBlnc, 0)
            ELSE 0 
        END
    ), 0)
    FROM dbo.OpeningBalanceDetails obd
    INNER JOIN dbo.OpeningBalances ob ON ob.Id = obd.OBId
    WHERE obd.COAId = @ChartOfAccountId
      AND (@FinancialYearId IS NULL OR ob.FinancialYearId = @FinancialYearId OR ob.FinancialYearId IS NULL)
      AND (@CompanyId IS NULL OR ob.CompanyId = @CompanyId OR ob.CompanyId = 0)
      AND (ob.Active IS NULL OR ob.Active = 1);

    -- 4. Calculate Prior Period Transactions (ExtraDate < @FromDateTime)
    DECLARE @PriorPeriodBal DECIMAL(18, 4) = 0;

    SELECT @PriorPeriodBal = ISNULL(SUM(ISNULL(at.Debit, 0) - ISNULL(at.Credit, 0)), 0)
    FROM dbo.AccountTransaction at
    WHERE at.ChartofAccountId = @ChartOfAccountId
      AND at.ExtraDate < @FromDateTime
      AND (@CompanyId IS NULL OR at.CompanyId = @CompanyId OR at.CompanyId = 0 OR at.CompanyId IS NULL);

    -- Total Opening Balance brought forward into this period
    DECLARE @TotalOpeningBal DECIMAL(18, 4) = @VoucherOpeningBal + @PriorPeriodBal;

    -- 5. Calculate Period Totals and Final Outstanding Balance
    DECLARE @PeriodDebit DECIMAL(18, 4) = 0;
    DECLARE @PeriodCredit DECIMAL(18, 4) = 0;

    SELECT 
        @PeriodDebit = ISNULL(SUM(ISNULL(at.Debit, 0)), 0),
        @PeriodCredit = ISNULL(SUM(ISNULL(at.Credit, 0)), 0)
    FROM dbo.AccountTransaction at
    WHERE at.ChartofAccountId = @ChartOfAccountId
      AND at.ExtraDate >= @FromDateTime 
      AND at.ExtraDate <= @ToDateTime
      AND (@CompanyId IS NULL OR at.CompanyId = @CompanyId OR at.CompanyId = 0 OR at.CompanyId IS NULL);

    DECLARE @OutstandingBal DECIMAL(18, 4) = @TotalOpeningBal + (@PeriodDebit - @PeriodCredit);

    -- 6. Collect In-Period Transactions
    -- Use IF EXISTS / ELSE to guarantee EXACTLY ONE single result set is returned to client / ADO.NET
    IF EXISTS (
        SELECT 1 
        FROM dbo.AccountTransaction at
        WHERE at.ChartofAccountId = @ChartOfAccountId
          AND at.ExtraDate >= @FromDateTime 
          AND at.ExtraDate <= @ToDateTime
          AND (@CompanyId IS NULL OR at.CompanyId = @CompanyId OR at.CompanyId = 0 OR at.CompanyId IS NULL)
    )
    BEGIN
        ;WITH InPeriodTx AS (
            SELECT 
                at.AccountTransactionId,
                at.ExtraDate AS [Date],
                at.VoucherNo,
                at.VoucherId,
                at.InvoiceNo,
                COALESCE(at.EntryTypeName, at.[Type], 'Voucher') AS VoucherType,
                ISNULL(at.Narration, '') AS Remarks,
                at.ChequeNo,
                at.ChequeDate,
                ISNULL(at.Debit, 0) AS Debit,
                ISNULL(at.Credit, 0) AS Credit
            FROM dbo.AccountTransaction at
            WHERE at.ChartofAccountId = @ChartOfAccountId
              AND at.ExtraDate >= @FromDateTime 
              AND at.ExtraDate <= @ToDateTime
              AND (@CompanyId IS NULL OR at.CompanyId = @CompanyId OR at.CompanyId = 0 OR at.CompanyId IS NULL)
        ),
        NumberedTx AS (
            SELECT 
                t.AccountTransactionId,
                t.[Date],
                t.VoucherNo,
                t.VoucherId,
                t.InvoiceNo,
                t.VoucherType,
                t.Remarks,
                t.ChequeNo,
                t.ChequeDate,
                t.Debit,
                t.Credit,
                @TotalOpeningBal + SUM(t.Debit - t.Credit) OVER (
                    ORDER BY t.[Date], t.VoucherId, t.AccountTransactionId 
                    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                ) AS RunningBalance
            FROM InPeriodTx t
        )
        SELECT 
            n.AccountTransactionId,
            n.[Date],
            n.VoucherNo,
            n.VoucherId,
            n.InvoiceNo,
            n.VoucherType,
            n.Remarks,
            n.ChequeNo,
            n.ChequeDate,
            n.Debit,
            n.Credit,
            n.RunningBalance AS Balance,
            @TotalOpeningBal AS OpeningBalance,
            @OutstandingBal AS OutstandingBalance,
            @PeriodDebit AS TotalPeriodDebit,
            @PeriodCredit AS TotalPeriodCredit,
            @ChartOfAccountId AS ChartOfAccountId,
            @AccountCode AS AccountCode,
            @AccountName AS AccountName,
            @NatureName AS NatureName,
            @CompanyName AS CompanyName
        FROM NumberedTx n
        ORDER BY n.[Date], n.VoucherId, n.AccountTransactionId;
    END
    ELSE
    BEGIN
        SELECT 
            0 AS AccountTransactionId,
            @FromDateTime AS [Date],
            NULL AS VoucherNo,
            NULL AS VoucherId,
            NULL AS InvoiceNo,
            'OB' AS VoucherType,
            'No transactions in selected period' AS Remarks,
            NULL AS ChequeNo,
            NULL AS ChequeDate,
            0.0000 AS Debit,
            0.0000 AS Credit,
            @TotalOpeningBal AS Balance,
            @TotalOpeningBal AS OpeningBalance,
            @OutstandingBal AS OutstandingBalance,
            0.0000 AS TotalPeriodDebit,
            0.0000 AS TotalPeriodCredit,
            @ChartOfAccountId AS ChartOfAccountId,
            @AccountCode AS AccountCode,
            @AccountName AS AccountName,
            @NatureName AS NatureName,
            @CompanyName AS CompanyName;
    END
END
GO
