USE tmpdata;

--
-- Table structure for table `Auth_PasswordReset`
--

CREATE TABLE IF NOT EXISTS `Auth_PasswordReset` (
  `UserID` varchar(40) NOT NULL,
  `TokenHash` binary(32) NOT NULL,
  `ExpiresOnUTC` datetime NOT NULL,
  `CreatedOnUTC` datetime NOT NULL,
  PRIMARY KEY (`UserID`),
  UNIQUE KEY `UserID_UNIQUE` (`UserID`),
  UNIQUE KEY `TokenHash_UNIQUE` (`TokenHash`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
