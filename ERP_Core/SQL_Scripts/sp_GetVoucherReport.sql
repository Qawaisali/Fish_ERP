-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetVoucherReport
-- Description: Retrieves detailed Voucher and Double-Entry Transaction Lines
--              filtered by Date Range, Voucher Type (BP, CP, BR, CR, JV),
--              Specific Voucher, and Chart of Accounts.
-- ==========================================================================================

IF OBJECT_ID('dbo.sp_GetVoucherReport', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetVoucherReport;
GO

CREATE PROCEDURE dbo.sp_GetVoucherReport
    @CompanyId INT = 1,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @VoucherType VARCHAR(20) = NULL,
    @VoucherNo VARCHAR(50) = NULL,
    @ChartOfAccountId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Date Range defaults (Defaults to today)
    IF @FromDate IS NULL
        SET @FromDate = CAST(GETDATE() AS DATE);

    IF @ToDate IS NULL
        SET @ToDate = CAST(GETDATE() AS DATE);

    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @ToDateTime DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@ToDate AS DATETIME)));

    -- 2. Normalize filter parameters
    SET @VoucherType = NULLIF(LTRIM(RTRIM(@VoucherType)), '');
    IF @VoucherType = 'ALL' SET @VoucherType = NULL;

    SET @VoucherNo = NULLIF(LTRIM(RTRIM(@VoucherNo)), '');
    IF @VoucherNo = 'ALL' SET @VoucherNo = NULL;

    IF @ChartOfAccountId <= 0 SET @ChartOfAccountId = NULL;

    -- 3. Base Filtered Vouchers CTE
    ;WITH FilteredVouchers AS (
        SELECT 
            v.VoucherId,
            v.VoucherNo,
            v.VoucherDate,
            v.VoucherType,
            v.Amount,
            v.AmountInWords,
            v.Narration AS VoucherNarration,
            v.ApprovalStatus,
            v.CreatedBy,
            v.CreatedOn,
            v.CompanyId
        FROM dbo.Voucher v
        WHERE (v.CompanyId = @CompanyId OR @CompanyId IS NULL)
          AND ISNULL(v.DeleteStatus, 0) = 0
          AND v.VoucherDate >= @FromDateTime
          AND v.VoucherDate <= @ToDateTime
          -- Voucher Type filter: BP, CP, BR, CR, JV (handling common aliases like BPV, CPV, etc.)
          AND (
              @VoucherType IS NULL 
              OR (@VoucherType = 'BP' AND (v.VoucherType IN ('BP', 'BPV') OR v.VoucherNo LIKE 'BP%'))
              OR (@VoucherType = 'CP' AND (v.VoucherType IN ('CP', 'CPV') OR v.VoucherNo LIKE 'CP%'))
              OR (@VoucherType = 'BR' AND (v.VoucherType IN ('BR', 'BRV') OR v.VoucherNo LIKE 'BR%'))
              OR (@VoucherType = 'CR' AND (v.VoucherType IN ('CR', 'CRV') OR v.VoucherNo LIKE 'CR%'))
              OR (@VoucherType = 'JV' AND (v.VoucherType = 'JV' OR v.VoucherNo LIKE 'JV%'))
              OR (v.VoucherType = @VoucherType)
          )
          -- Specific Voucher No filter
          AND (@VoucherNo IS NULL OR v.VoucherNo = @VoucherNo)
          -- Chart of Account filter: Include voucher if it touches this Chart of Account
          AND (
              @ChartOfAccountId IS NULL 
              OR EXISTS (
                  SELECT 1 
                  FROM dbo.AccountTransaction at_chk 
                  WHERE (at_chk.VoucherId = v.VoucherId OR at_chk.VoucherNo = v.VoucherNo)
                    AND at_chk.ChartofAccountId = @ChartOfAccountId
              )
          )
    )

    -- 4. Result Set 1: Summary Statistics (KPI Cards)
    SELECT 
        COUNT(DISTINCT fv.VoucherId) AS TotalVouchers,
        ISNULL(SUM(at.Debit), 0.00) AS TotalDebit,
        ISNULL(SUM(at.Credit), 0.00) AS TotalCredit,
        COUNT(at.AccountTransactionId) AS TotalLines,
        ISNULL(SUM(CASE WHEN at.Debit > 0 THEN at.Debit ELSE 0 END), 0.00) AS TotalAmount
    FROM FilteredVouchers fv
    LEFT JOIN dbo.AccountTransaction at ON (at.VoucherId = fv.VoucherId OR (at.VoucherId IS NULL AND at.VoucherNo = fv.VoucherNo));

    -- 5. Result Set 2: Detailed Transaction Lines
    ;WITH FilteredVouchers AS (
        SELECT 
            v.VoucherId,
            v.VoucherNo,
            v.VoucherDate,
            v.VoucherType,
            v.Amount,
            v.AmountInWords,
            v.Narration AS VoucherNarration,
            v.ApprovalStatus,
            v.CreatedBy,
            v.CreatedOn,
            v.CompanyId
        FROM dbo.Voucher v
        WHERE (v.CompanyId = @CompanyId OR @CompanyId IS NULL)
          AND ISNULL(v.DeleteStatus, 0) = 0
          AND v.VoucherDate >= @FromDateTime
          AND v.VoucherDate <= @ToDateTime
          AND (
              @VoucherType IS NULL 
              OR (@VoucherType = 'BP' AND (v.VoucherType IN ('BP', 'BPV') OR v.VoucherNo LIKE 'BP%'))
              OR (@VoucherType = 'CP' AND (v.VoucherType IN ('CP', 'CPV') OR v.VoucherNo LIKE 'CP%'))
              OR (@VoucherType = 'BR' AND (v.VoucherType IN ('BR', 'BRV') OR v.VoucherNo LIKE 'BR%'))
              OR (@VoucherType = 'CR' AND (v.VoucherType IN ('CR', 'CRV') OR v.VoucherNo LIKE 'CR%'))
              OR (@VoucherType = 'JV' AND (v.VoucherType = 'JV' OR v.VoucherNo LIKE 'JV%'))
              OR (v.VoucherType = @VoucherType)
          )
          AND (@VoucherNo IS NULL OR v.VoucherNo = @VoucherNo)
          AND (
              @ChartOfAccountId IS NULL 
              OR EXISTS (
                  SELECT 1 
                  FROM dbo.AccountTransaction at_chk 
                  WHERE (at_chk.VoucherId = v.VoucherId OR at_chk.VoucherNo = v.VoucherNo)
                    AND at_chk.ChartofAccountId = @ChartOfAccountId
              )
          )
    )
    SELECT 
        fv.VoucherId,
        fv.VoucherNo,
        fv.VoucherDate,
        UPPER(LTRIM(RTRIM(
            CASE 
                WHEN fv.VoucherType IN ('BP', 'BPV') OR fv.VoucherNo LIKE 'BP%' THEN 'BP'
                WHEN fv.VoucherType IN ('CP', 'CPV') OR fv.VoucherNo LIKE 'CP%' THEN 'CP'
                WHEN fv.VoucherType IN ('BR', 'BRV') OR fv.VoucherNo LIKE 'BR%' THEN 'BR'
                WHEN fv.VoucherType IN ('CR', 'CRV') OR fv.VoucherNo LIKE 'CR%' THEN 'CR'
                WHEN fv.VoucherType = 'JV' OR fv.VoucherNo LIKE 'JV%' THEN 'JV'
                ELSE ISNULL(fv.VoucherType, 'Voucher')
            END
        ))) AS VoucherType,
        ISNULL(fv.Amount, 0.00) AS VoucherAmount,
        fv.AmountInWords,
        fv.VoucherNarration,
        ISNULL(fv.ApprovalStatus, 'Approved') AS ApprovalStatus,
        fv.CreatedBy,
        fv.CreatedOn,
        at.AccountTransactionId,
        at.ChartofAccountId,
        COALESCE(at.Code, coa.AccountCode, '') AS AccountCode,
        COALESCE(at.AccountName, coa.AccountName, at.ChartofAccountName, 'General Account') AS AccountName,
        ISNULL(at.Debit, 0.00) AS Debit,
        ISNULL(at.Credit, 0.00) AS Credit,
        COALESCE(at.Narration, fv.VoucherNarration, '') AS Narration,
        at.ChequeNo,
        at.ChequeDate,
        at.InvoiceNo,
        CASE WHEN at.ChartofAccountId = @ChartOfAccountId THEN 1 ELSE 0 END AS IsFilteredAccount
    FROM FilteredVouchers fv
    LEFT JOIN dbo.AccountTransaction at ON (at.VoucherId = fv.VoucherId OR (at.VoucherId IS NULL AND at.VoucherNo = fv.VoucherNo))
    LEFT JOIN dbo.ChartOfAccounts coa ON coa.Id = at.ChartofAccountId
    ORDER BY fv.VoucherDate DESC, fv.VoucherId DESC, at.Debit DESC, at.AccountTransactionId ASC;
END
GO
