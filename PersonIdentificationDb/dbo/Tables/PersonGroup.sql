CREATE TABLE [dbo].[PersonGroup] (
    [PersonGroupId] UNIQUEIDENTIFIER NOT NULL,
    [PersonGroupName] VARCHAR (50)   NOT NULL,
    [IsTrained]     BIT              NULL,
    [IsDeleted]     BIT              NULL,
    [CreatedBy]     VARCHAR (50)     NOT NULL DEFAULT 'system',
    [CreatedDate]   DATETIME         DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]    VARCHAR (50)     NULL,
    [ModifiedDate]  DATETIME         NULL,
    PRIMARY KEY CLUSTERED ([PersonGroupId] ASC)
);