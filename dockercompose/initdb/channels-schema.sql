use tmpdata;

CREATE TABLE `CMS_Category` (
  `ContentID` varchar(40) NOT NULL,
  `CategoryID` varchar(40) NOT NULL,
  PRIMARY KEY (`ContentID`,`CategoryID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;

CREATE TABLE `CMS_Channel` (
  `ContentID` varchar(40) NOT NULL,
  `ChannelID` varchar(40) NOT NULL,
  PRIMARY KEY (`ContentID`,`ChannelID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;