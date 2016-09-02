USE [UserData]
GO

/****** Object:  Table [dbo].[UserInfo]    Script Date: 08/31/2016 18:30:46 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[UserInfo](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NULL,
	[TrueName] [nvarchar](50) NULL,
	[MobilePhone] [nvarchar](30) NULL,
	[HomePhone] [nvarchar](30) NULL,
	[Email] [nvarchar](50) NULL,
	[Address] [nvarchar](100) NULL,
	[CityName] [nvarchar](50) NULL,
	[RegionName] [nvarchar](50) NULL,
	[CreatTime] [datetime] NULL,
	[ModityTime] [datetime] NULL,
 CONSTRAINT [PK_UserInfo] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO


