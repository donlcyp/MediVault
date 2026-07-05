-- Enable all existing users (set IsEnabled = 1 for all users)
UPDATE AspNetUsers 
SET IsEnabled = 1 
WHERE IsEnabled = 0;

-- Check the result
SELECT Email, IsEnabled FROM AspNetUsers;