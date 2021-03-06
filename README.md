# BrickCostMinimizer
This is a .NET Console application that takes a list of items in [Brickstock](http://brickstock.patrickbrans.com/) XML format, and then finds the cheapest combination of sellers on [Bricklink](http://www.bricklink.com). The maximum number of sellers is configurable.

## Getting started
The code should build on any version of Visual Studio from 2013 onwards. Once built, the following settings can be configured if required  in the BrickCostMinimizer.exe.config file:

- MaxThreads: The number of worker threads that process results. Default is 3, which is suitable for a quad core system.
- If a proxy server is required, this can be configured here.

The program uses a cached data folder and a results folder where the HTML output is saved. By default, these are created in the application folder but it's possible to override the locations in the config file with the following settings:

- BricklinkData: A folder location to temporarily cache data from Bricklink.
- ResultsFolder: A folder where the HTML output should be written.

Once the application is built and any settings applied, it can be run with any XML file saved from Brickstock, using the following arguments:

    BrickCostMinimizer item_xml_path max_sellers

Recommended values for the maximum number of sellers are 2 to 4, the exact value will depend on the number of items in the list. In general, up to 3 sellers can be run fairly quickly (1 or 2 minutes), 4 sellers can take much longer. Increasing the minimum number of items from the list that a seller must have before they're considered can help.

## Limitations

This program was written primarily as a personal project to experiment with the searching algorithm, and has some limitations, both in its design and fuctionality:

- The HTML scraping code is currently implemented with regular expressions. Plans are in place to migrate this to HtmlAgilityPack.
- The HTML output is currently hard coded - I'd like to replace it with the Razor template engine.
- Currently only UK sellers are considered, and the Bricklink cookies are set to always retrieve results in GBP.
- There are currently no unit tests - I hope to remedy this soon
