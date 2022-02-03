# er
Edited Raw. Windows command line tool for copying RAW image files edited in Adobe produts including Lightroom.

I have an old 4TB external drive I wanted to use for backup. My RAW files consume about 5.4TB. But I've only edited 
about 2TB of those files. This app enables me to backup those RAW files I've actually edited along with those 
Lightroom edits. It looks at the .xmp files I've configured Lightroom to create, extracts the RAW filename, then
copies both to the destination location.


Build with your favorite version of .net. For example:

    c:\windows\microsoft.net\framework64\v4.0.30319\csc.exe er.cs /nologo
    
Usage

    Usage: er <sourcepath> <destinationpath>
    Edited Raw file copy app
      example: er c:\users\david\pictures x:\backup\pictures
               er d:\ x:\backup\pictures
      notes:
        This app recursively looks for lightroom .xmp files then copies those and the raw images they reference.
        This enables backup of images you care about (and have edited) vs. those not edited.
        .dng RAW files (Ricoh, Leica, etc.) store Lightroom data. They have no .xmp. Backup these separately.
        Same for .jpg, .tif, .tiff. -- Lightroom stores edits in the files, not separate .xmp files.
