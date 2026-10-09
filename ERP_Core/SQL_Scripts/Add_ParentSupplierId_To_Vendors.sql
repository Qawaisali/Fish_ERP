-- =========================================================================
-- Script: Add_ParentSupplierId_To_Vendors.sql
-- Description: Adds nullable ParentSupplierId column and FK constraint to Vendors
--              table to support Parent Supplier / Subordinate Employee relationship.
-- =========================================================================

IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Vendors]') 
    AND name = 'ParentSupplierId'
)
BEGIN
    PRINT 'Adding column ParentSupplierId to dbo.Vendors...';
    ALTER TABLE [dbo].[Vendors] ADD [ParentSupplierId] INT NULL;
    PRINT 'Column ParentSupplierId added successfully.';
END
ELSE
BEGIN
    PRINT 'Column ParentSupplierId already exists in dbo.Vendors.';
END
GO

-- Add Foreign Key Constraint if not already present
IF NOT EXISTS (
    SELECT 1 
    FROM sys.foreign_keys 
    WHERE object_id = OBJECT_ID(N'[dbo].[FK_Vendors_Vendors_ParentSupplierId]')
)
BEGIN
    PRINT 'Adding Foreign Key constraint FK_Vendors_Vendors_ParentSupplierId...';
    ALTER TABLE [dbo].[Vendors] WITH CHECK 
    ADD CONSTRAINT [FK_Vendors_Vendors_ParentSupplierId] 
    FOREIGN KEY([ParentSupplierId]) REFERENCES [dbo].[Vendors] ([Id]);

    ALTER TABLE [dbo].[Vendors] CHECK CONSTRAINT [FK_Vendors_Vendors_ParentSupplierId];
    PRINT 'Foreign Key constraint FK_Vendors_Vendors_ParentSupplierId created successfully.';
END
ELSE
BEGIN
    PRINT 'Foreign Key constraint FK_Vendors_Vendors_ParentSupplierId already exists.';
END
GO
