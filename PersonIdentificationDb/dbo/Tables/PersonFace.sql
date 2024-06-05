CREATE TABLE [dbo].[PersonFace]
(
	[FaceId]             UNIQUEIDENTIFIER NOT NULL,
    [PersonId]           UNIQUEIDENTIFIER NOT NULL,
	[CreatedBy]          VARCHAR (50)     NOT NULL,
    [CreatedDate]        DATETIME         DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]         VARCHAR (50)     NULL,
    [ModifiedDate]       DATETIME         NULL,
    PRIMARY KEY CLUSTERED ([FaceId] ASC),
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[PersonGroupImage] ([PersonId])
);