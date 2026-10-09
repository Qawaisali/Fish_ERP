-- =========================================================================
-- Fix ROZANA KAAM SubModules ParentModuleId in CModules
-- =========================================================================

-- In CModules, records 38 (New Bill), 39 (Daily Purchase), 40 (Expense Voucher)
-- were inserted with ParentModuleId = 0 and isParentModule = 37.
-- This script corrects ParentModuleId to 37 (ROZANA KAAM), isParentModule = 0, and isSubModule = 1.

UPDATE [dbo].[CModules]
SET [ParentModuleId] = 37,
    [isParentModule] = 0,
    [isSubModule] = 1
WHERE [Id] IN (38, 39, 40);

-- General safeguard: any record where isParentModule > 1 has its ParentModuleId set to that value
UPDATE [dbo].[CModules]
SET [ParentModuleId] = [isParentModule],
    [isParentModule] = 0,
    [isSubModule] = 1
WHERE [ParentModuleId] = 0 AND [isParentModule] > 1;

-- Verify results
SELECT [Id], [Name], [Url], [ParentModuleId], [isParentModule], [isSubModule], [Order]
FROM [dbo].[CModules]
WHERE [Id] IN (30, 1, 37, 38, 39, 40, 31, 32, 33, 34, 35)
ORDER BY [Order] ASC;
