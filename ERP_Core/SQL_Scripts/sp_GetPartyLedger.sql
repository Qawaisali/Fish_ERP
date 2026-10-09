-- ==========================================================================================
-- Stored Procedure: dbo.sp_GetPartyLedger
-- Description: Unified Stored Procedure for Party (Supplier and Customer) Ledger.
--              Supplier: Payable balance (rises on Credit)
--              Customer: Receivable balance (rises on Debit)
-- ==========================================================================================

IF OBJECT_ID('[dbo].[sp_GetPartyLedger]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetPartyLedger];
GO

CREATE PROCEDURE [dbo].[sp_GetPartyLedger]
    @CompanyId        INT,
    @PartyId          INT,
    @FromDate         DATE = NULL,
    @ToDate           DATE = NULL,
    @FinancialYearId  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PartyName      NVARCHAR(250) = N'';
    DECLARE @VendorType     NVARCHAR(50)  = N'';
    DECLARE @PartyCOAId     INT           = NULL;
    DECLARE @LegacyOB       DECIMAL(18,2) = 0.00;
    DECLARE @VchrOB         DECIMAL(18,2) = 0.00;
    DECLARE @InitialBal     DECIMAL(18,2) = 0.00;
    DECLARE @CompanyName    NVARCHAR(250) = N'';
    DECLARE @CompanyAddress NVARCHAR(500) = N'';
    DECLARE @Logo           NVARCHAR(500) = N'';

    SELECT
        @PartyName  = ISNULL(v.Name, N''),
        @VendorType = ISNULL(v.VendorType, N''),
        @PartyCOAId = v.ChartOfAccountId,
        @LegacyOB   = ISNULL(v.OpeningBalance, 0.00)
    FROM dbo.Vendors v
    WHERE v.Id = @PartyId
      AND (v.CompanyId = @CompanyId OR @CompanyId IS NULL);

    -- Supplier = payable, balance rises on Credit. Everything else (Customer)
    -- = receivable, balance rises on Debit.
    DECLARE @Sign INT = CASE WHEN UPPER(LTRIM(RTRIM(@VendorType))) = 'SUPPLIER' THEN -1 ELSE 1 END;

    SELECT
        @CompanyName    = ISNULL(c.CompanyName, N''),
        @CompanyAddress = ISNULL(c.Address, N''),
        @Logo           = ISNULL(c.Logo, N'')
    FROM dbo.CCompanies c
    WHERE c.CompanyId = @CompanyId;

    IF @PartyCOAId IS NOT NULL
    BEGIN
        SELECT @VchrOB = ISNULL(SUM(
            CASE
                WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'DEB' THEN  @Sign * ISNULL(obd.OBDtlOpenBlnc, 0)
                WHEN UPPER(LTRIM(RTRIM(obd.OBDtlTranType))) = 'CRE' THEN -@Sign * ISNULL(obd.OBDtlOpenBlnc, 0)
                ELSE 0
            END), 0)
        FROM dbo.OpeningBalanceDetails obd
        INNER JOIN dbo.OpeningBalances ob ON obd.OBId = ob.Id
        WHERE obd.COAId = @PartyCOAId
          AND (ob.CompanyId = @CompanyId OR @CompanyId IS NULL OR ob.CompanyId = 0)
          AND ob.FinancialYearId = @FinancialYearId
          AND (ob.Active IS NULL OR ob.Active = 1);
    END;

    SET @InitialBal = (@Sign * @LegacyOB) + @VchrOB;

    IF OBJECT_ID('tempdb..#PartyTxns') IS NOT NULL DROP TABLE #PartyTxns;

    CREATE TABLE #PartyTxns (
        AccountTransactionId INT,
        VoucherId            INT,
        VoucherCode          VARCHAR(50),
        InvoiceNo             NVARCHAR(100),
        Narration             NVARCHAR(1000),
        TxnDate               DATE,
        ChequeNo              NVARCHAR(50),
        ChequeDate            DATETIME,
        DebitAmount           DECIMAL(18,2),
        CreditAmount          DECIMAL(18,2)
    );

    IF @PartyCOAId IS NOT NULL
    BEGIN
        INSERT INTO #PartyTxns (
            AccountTransactionId, VoucherId, VoucherCode, InvoiceNo,
            Narration, TxnDate, ChequeNo, ChequeDate, DebitAmount, CreditAmount
        )
        SELECT
            at.AccountTransactionId,
            v.VoucherId,
            COALESCE(v.VoucherNo, at.VoucherNo, N'') AS VoucherCode,
            at.InvoiceNo,
            ISNULL(at.Narration, ISNULL(at.EntryTypeName, N'Transaction')),
            CAST(ISNULL(at.ExtraDate, v.VoucherDate) AS DATE),
            at.ChequeNo,
            at.ChequeDate,
            ISNULL(at.Debit, 0.00),
            ISNULL(at.Credit, 0.00)
        FROM dbo.AccountTransaction at
        INNER JOIN dbo.Voucher v ON v.VoucherId = at.VoucherId
        WHERE (at.CompanyId = @CompanyId OR @CompanyId IS NULL)
          AND at.ChartofAccountId = @PartyCOAId
          AND ISNULL(v.DeleteStatus, 0) = 0
          AND (v.ApprovalStatus = 'Approved' OR v.ApprovalStatus IS NULL)
          AND v.FinancialYearID = @FinancialYearId;
    END;

    DECLARE @PriorPeriodMovement DECIMAL(18,2) = 0.00;
    IF @FromDate IS NOT NULL
    BEGIN
        SELECT @PriorPeriodMovement = ISNULL(SUM(@Sign * (DebitAmount - CreditAmount)), 0.00)
        FROM #PartyTxns
        WHERE TxnDate < @FromDate;
    END;

    DECLARE @TotalOpeningBalance DECIMAL(18,2) = @InitialBal + @PriorPeriodMovement;

    IF OBJECT_ID('tempdb..#InRangeTxns') IS NOT NULL DROP TABLE #InRangeTxns;

    SELECT *
    INTO #InRangeTxns
    FROM #PartyTxns
    WHERE (@FromDate IS NULL OR TxnDate >= @FromDate)
      AND (@ToDate   IS NULL OR TxnDate <= @ToDate);

    DECLARE @PeriodMovement DECIMAL(18,2) = 0.00;
    SELECT @PeriodMovement = ISNULL(SUM(@Sign * (DebitAmount - CreditAmount)), 0.00) FROM #InRangeTxns;

    DECLARE @OutstandingBalance DECIMAL(18,2) = @TotalOpeningBalance + @PeriodMovement;

    IF EXISTS (SELECT 1 FROM #InRangeTxns)
    BEGIN
        SELECT
            @PartyName            AS [Name],
            @VendorType           AS [PartyType],
            @TotalOpeningBalance  AS [OpeningBalance],
            ir.TxnDate            AS [Date],
            ir.InvoiceNo          AS [InvoiceNo],
            ir.VoucherCode        AS [VoucherCode],
            ir.Narration          AS [Remarks],
            ir.ChequeNo           AS [ChqNo],
            ir.ChequeDate         AS [ChqDate],
            NULLIF(ir.DebitAmount, 0)  AS [Debit],
            NULLIF(ir.CreditAmount, 0) AS [Credit],
            @TotalOpeningBalance
                + SUM(@Sign * (ir.DebitAmount - ir.CreditAmount))
                    OVER (ORDER BY ir.TxnDate, ir.VoucherId, ir.AccountTransactionId
                          ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS [Balance],
            @OutstandingBalance   AS [OutstandingBalance],
            @CompanyName          AS [CompanyName],
            @CompanyAddress       AS [CompanyAddress],
            @Logo                 AS [Logo]
        FROM #InRangeTxns ir
        ORDER BY ir.TxnDate, ir.VoucherId, ir.AccountTransactionId;
    END
    ELSE
    BEGIN
        SELECT
            @PartyName            AS [Name],
            @VendorType           AS [PartyType],
            @TotalOpeningBalance  AS [OpeningBalance],
            ISNULL(@FromDate, CAST(GETDATE() AS DATE)) AS [Date],
            NULL AS [InvoiceNo], NULL AS [VoucherCode],
            N'No transactions in selected period' AS [Remarks],
            NULL AS [ChqNo], NULL AS [ChqDate],
            NULL AS [Debit], NULL AS [Credit],
            @TotalOpeningBalance  AS [Balance],
            @OutstandingBalance   AS [OutstandingBalance],
            @CompanyName          AS [CompanyName],
            @CompanyAddress       AS [CompanyAddress],
            @Logo                 AS [Logo];
    END;

    IF OBJECT_ID('tempdb..#PartyTxns') IS NOT NULL DROP TABLE #PartyTxns;
    IF OBJECT_ID('tempdb..#InRangeTxns') IS NOT NULL DROP TABLE #InRangeTxns;
END;
GO
