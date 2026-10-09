-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetAccountTransactions
-- Description: Retrieves SUMMED transactions grouped ONE ROW PER CHART OF ACCOUNT,
--              filtered strictly by transaction Type and Date Range.
--              Excludes accounts with zero transactions to avoid clutter.
--              Opening balances are omitted as requested.
-- ==========================================================================================

CREATE OR ALTER PROCEDURE dbo.sp_GetAccountTransactions
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @Type VARCHAR(50) = NULL,
    @Search NVARCHAR(100) = NULL,
    @ChartOfAccountId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Date bounds
    DECLARE @FromDateTime DATETIME = CASE WHEN @FromDate IS NOT NULL THEN CAST(@FromDate AS DATETIME) ELSE '1900-01-01' END;
    DECLARE @ToDateTime DATETIME = CASE WHEN @ToDate IS NOT NULL THEN DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME))) ELSE '2099-12-31 23:59:59' END;

    SET @Type = NULLIF(LTRIM(RTRIM(@Type)), '');
    IF UPPER(@Type) = 'ALL' SET @Type = NULL;

    SET @Search = NULLIF(LTRIM(RTRIM(@Search)), '');
    IF @ChartOfAccountId <= 0 SET @ChartOfAccountId = NULL;

    -- Bank & Cash Chart of Account IDs to exclude for party-centric queries
    DECLARE @BankCoaIds TABLE (ChartOfAccountId INT PRIMARY KEY);
    INSERT INTO @BankCoaIds (ChartOfAccountId)
    SELECT DISTINCT ChartOfAccountId FROM dbo.BankMasters WHERE ChartOfAccountId IS NOT NULL;

    -- 1. Filtered Transactions CTE
    ;WITH FilteredTx AS (
        SELECT 
            at.AccountTransactionId,
            at.ChartofAccountId,
            ISNULL(at.Debit, 0) AS Debit,
            ISNULL(at.Credit, 0) AS Credit,
            ISNULL(at.Amount, 0) AS Amount,
            at.ExtraDate,
            at.AccountName,
            at.ChartofAccountName,
            at.VoucherNo
        FROM dbo.AccountTransaction at WITH (NOLOCK)
        LEFT JOIN dbo.ChartOfAccounts coa WITH (NOLOCK) ON at.ChartofAccountId = coa.Id
        WHERE (at.CompanyId = @CompanyId OR at.CompanyId IS NULL OR at.CompanyId = 0)
          AND (@ChartOfAccountId IS NULL OR at.ChartofAccountId = @ChartOfAccountId)
          AND at.ExtraDate >= @FromDateTime AND at.ExtraDate <= @ToDateTime
          -- Filter by Type exactly matching dashboard logic
          AND (
              @Type IS NULL
              OR (
                  UPPER(@Type) = 'ADVANCE' AND (
                      UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'ADVANCE' 
                      OR at.Narration LIKE '%[[]Advance]%' 
                      OR at.Narration LIKE '%Advance%' 
                      OR at.EntryTypeName LIKE '%Advance%' 
                      OR at.ChartofAccountName LIKE '%Advance%' 
                      OR coa.AccountName LIKE '%Advance%'
                  )
                  AND (at.ChartofAccountId IS NULL OR at.ChartofAccountId NOT IN (SELECT ChartOfAccountId FROM @BankCoaIds))
                  AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
              )
              OR (
                  UPPER(@Type) = 'MARTEY' AND (
                      UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'MARTEY' 
                      OR (at.Narration LIKE '%Martey%' AND at.Narration NOT LIKE '%Martey: Rs 0%') 
                      OR at.EntryTypeName LIKE '%Martey%'
                  )
                  AND (at.ChartofAccountId IS NULL OR at.ChartofAccountId NOT IN (SELECT ChartOfAccountId FROM @BankCoaIds))
                  AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
              )
              OR (
                  UPPER(@Type) = 'LOAN' AND (
                      UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'LOAN' 
                      OR at.Narration LIKE '%[[]Loan]%' 
                      OR at.Narration LIKE '%Loan%' 
                      OR at.EntryTypeName LIKE '%Loan%' 
                      OR at.ChartofAccountName LIKE '%Loan%' 
                      OR coa.AccountName LIKE '%Loan%'
                  )
                  AND (at.ChartofAccountId IS NULL OR at.ChartofAccountId NOT IN (SELECT ChartOfAccountId FROM @BankCoaIds))
                  AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
              )
              OR (
                  UPPER(@Type) = 'OTHER' AND (
                      UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'OTHER' 
                      OR at.Narration LIKE '%[[]Other]%' 
                      OR at.Narration LIKE '%Other%' 
                      OR at.EntryTypeName LIKE '%Other%' 
                      OR at.ChartofAccountName LIKE '%Other%' 
                      OR coa.AccountName LIKE '%Other%'
                  )
                  AND (at.ChartofAccountId IS NULL OR at.ChartofAccountId NOT IN (SELECT ChartOfAccountId FROM @BankCoaIds))
                  AND ISNULL(coa.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.AccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(at.ChartofAccountName, '') NOT LIKE '%Cash%'
                  AND ISNULL(coa.AccountCode, '') NOT LIKE '0101%'
              )
              OR (
                  UPPER(@Type) = 'COMMISSION' AND (
                      UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'COMMISSION' 
                      OR at.Narration LIKE '%Commission%' 
                      OR at.ChartofAccountName LIKE '%Commission%'
                  )
              )
              OR (
                  UPPER(@Type) = 'EXPENSE' AND (
                      UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = 'EXPENSE' 
                      OR at.Narration LIKE '%Expense%' 
                      OR at.ChartofAccountName LIKE '%Expense%' 
                      OR at.ChartofAccountName LIKE '%Kharch%' 
                      OR ISNULL(coa.AccountCode, '') LIKE '05%'
                  )
              )
              OR (
                  UPPER(@Type) NOT IN ('ADVANCE', 'MARTEY', 'LOAN', 'OTHER', 'COMMISSION', 'EXPENSE') 
                  AND UPPER(LTRIM(RTRIM(ISNULL(at.Type, '')))) = UPPER(@Type)
              )
          )
    ),
    -- 2. Aggregate strictly by Chart of Account, requiring at least one transaction
    Aggregated AS (
        SELECT 
            ft.ChartofAccountId,
            COALESCE(
                NULLIF(LTRIM(RTRIM(MAX(v.Name))), ''),
                NULLIF(LTRIM(RTRIM(MAX(coa.AccountName))), ''),
                NULLIF(LTRIM(RTRIM(MAX(ft.AccountName))), ''),
                NULLIF(LTRIM(RTRIM(MAX(ft.ChartofAccountName))), ''),
                'Account #' + CAST(ft.ChartofAccountId AS VARCHAR(20))
            ) AS AccountTitle,
            MAX(coa.AccountCode) AS AccountCode,
            MAX(v.Id) AS VendorId,
            ISNULL(NULLIF(LTRIM(RTRIM(MAX(v.City))), ''), '-') AS City,
            ISNULL(NULLIF(LTRIM(RTRIM(MAX(v.Phone))), ''), '-') AS Phone,
            SUM(ft.Debit) AS TotalDebit,
            SUM(ft.Credit) AS TotalCredit,
            COUNT(ft.AccountTransactionId) AS VoucherCount,
            MAX(ft.ExtraDate) AS LastTxDate,
            MAX(ft.VoucherNo) AS LatestVoucherNo
        FROM FilteredTx ft
        LEFT JOIN dbo.ChartOfAccounts coa WITH (NOLOCK) ON ft.ChartofAccountId = coa.Id
        LEFT JOIN dbo.Vendors v WITH (NOLOCK) ON ft.ChartofAccountId = v.ChartOfAccountId AND (v.Active IS NULL OR v.Active = 1)
        WHERE ft.ChartofAccountId IS NOT NULL
        GROUP BY ft.ChartofAccountId
        HAVING (SUM(ft.Debit) > 0 OR SUM(ft.Credit) > 0 OR SUM(ft.Amount) > 0)
    )
    SELECT 
        a.ChartofAccountId,
        a.VendorId,
        a.AccountTitle AS AccountName,
        ISNULL(a.AccountCode, '') AS AccountCode,
        a.City,
        a.Phone,
        CAST(a.TotalDebit AS DECIMAL(18,2)) AS TotalDebit,
        CAST(a.TotalCredit AS DECIMAL(18,2)) AS TotalCredit,
        CAST(
            CASE 
                WHEN UPPER(ISNULL(@Type, '')) = 'MARTEY' THEN a.TotalCredit - a.TotalDebit
                WHEN UPPER(ISNULL(@Type, '')) = 'EXPENSE' THEN a.TotalDebit
                ELSE a.TotalDebit - a.TotalCredit
            END 
        AS DECIMAL(18,2)) AS NetBalance,
        a.VoucherCount,
        a.LastTxDate,
        a.LatestVoucherNo
    FROM Aggregated a
    WHERE (
        @Search IS NULL 
        OR a.AccountTitle LIKE '%' + @Search + '%'
        OR a.AccountCode LIKE '%' + @Search + '%'
        OR a.City LIKE '%' + @Search + '%'
        OR a.Phone LIKE '%' + @Search + '%'
    )
    ORDER BY 
        CASE 
            WHEN UPPER(ISNULL(@Type, '')) = 'MARTEY' THEN a.TotalCredit - a.TotalDebit
            ELSE a.TotalDebit - a.TotalCredit
        END DESC,
        a.TotalDebit DESC,
        a.AccountTitle ASC;
END;
GO
