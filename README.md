# DripDownloader
A quick and dirty tool to grab all available downloads from Drip before they shut down.

Works for me, may not work for you.
May destroy your computer (shouldn't, but you've been warned). 
Only downloads a releases that have a FLAC download option. 

Usage: dripdownloader emailaddress password path
Example: dripdownloader me@place.com bAdPAssW0d C:\dripbackup\

It will take a while to download everything. You can stop and restart it, and it will skip things it's already fetched.
If you stop in the middle of a download you may be left with a broken file, just delete the broken zip and restart. 

I'd clean up stuff like that but the tool will be usesless in a few days when Drip (sadly) shuts down anyway, so this is good enough.  

I'm not going to provide a binary (pre compiled) to download. 
If someone else wants to, that's great. 

To use, compile the solution using either Visual Studio or csc, find the exe, run it. 

Enjoy, 
R
