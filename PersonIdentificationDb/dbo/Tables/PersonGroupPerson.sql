CREATE TABLE [dbo].[PersonGroupPerson] (
    [PersonId]           UNIQUEIDENTIFIER NOT NULL,
    [PersonGroupId]      UNIQUEIDENTIFIER NOT NULL,
    [PersonName]         VARCHAR (50)     NULL,
    [CreatedBy]          VARCHAR (50)     NOT NULL DEFAULT 'system',
    [CreatedDate]        DATETIME         DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]         VARCHAR (50)     NULL,
    [ModifiedDate]       DATETIME         NULL,
    PRIMARY KEY CLUSTERED ([PersonId] ASC),
    FOREIGN KEY ([PersonGroupId]) REFERENCES [dbo].[PersonGroup] ([PersonGroupId])
);