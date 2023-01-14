# ExtractFaces readme
Extract face images from photos when they are taged with XMP like with Windows Photo Gallery, Digikam, Picasa, ...

Generates a single image for each person identified in an image XMP Metadata.

# Usage: Command ligne interface (CLI) 
The simplest way to call it would be
```
extractfaces -source:"C:\source_directory" -destination:"C:\destination_directory"
```
It will iterate over each photo in `C:\source_directory` and will generate an image corresponding to every face identified in them in `C:\destination_directory`.

From this image

![126-2679_IMG](https://user-images.githubusercontent.com/12274241/212491466-22716ea1-cdcf-4520-af0d-4d23c0f05991.JPG)

It will generate those files

![image](https://user-images.githubusercontent.com/12274241/212491571-41329422-1b21-4aba-a33f-263db34ce1a6.png)


## File parameters
### Source
```
-source:"C:\directory\photo.jpg" 
                OR 
-source:"C:\directory1\directory2"
```
The path to the source image to extract from or a directory name from which to extract all faces from all images.  

**NOTE**: that you should put the path between double quotes as in the exemple as a best practice.

### Destination
```
-destination 
```
The path to the directory where the new images will be created.  As for the source, please consider using the double quotes.

**IMPORTANT** : do not place the destination directory as a child or sub child of the source directory as it might iterate indefinetly.

### Recursive
```
-recursive
```
Apply this parameter to recursively visite every sub directory from the source.

## Generated images parameters
How do you want to generate your images.  If neither -flush nor -square parameter is specified, the application will assume -flush
### Flush
```
-flush
```
Will generate rectangular images that more closely encapsulate the face.  It's the default value.

### Square
```
-square
```
Will generate square image, that is that the width and height will be of same size.

### Person
```
-person:"Person Name|Second Person|Third Friend"
```
Specify the persons that you want to keep.  It will match the name specified in the image metadata.  If none is specified, it will generate for everybody.

**EXAMPLE**: If I specify -person:"Daniel Camiré|Valérie Camiré" in my previous example, I only generate those images

![image](https://user-images.githubusercontent.com/12274241/212491656-697c7880-8acb-4bba-bedd-62fcdc582e5c.png)


### Percent
```
-percent:"50|100|175|400"
```
Acts as a zooming factor for your faces.  

It will generate an image for each percent specified.  
* 50 means that it will be 50% (hence smaller) centered on the face.  
* 100 means that it will generate the exact region specified in the metadata
* 400 means that it will generate a square 4x larger then what is specified in the metadata.  The face will be smaller within and you will gain more contexte from the surrounding.

**EXAMPLE**: Still building on the same example, the setting `-percent:"50|100|175|400` yields those images

![image](https://user-images.githubusercontent.com/12274241/212491739-271ec35f-2f3e-459e-877e-eeb1fb978fd6.png)


## Log parameters
### What will be prompted in the console
```
-verbose
-showexif
-showxmp
```
If you specify verbose, you will have everything (a lot of loggin, and both the EXIF and the XML dump).  Use the other two if you are interested in logging them without all the logging overhead.  Note that the default will be to log only "warnings" and "errors".

### To wait at the end
```
-waitkey
```
The good old "Press any key to continue" 🤘

# Limitations
* It is built with .Net Standard 7 so you might be required to install that prior.
* Currently supports the Windows Photo Gallery standard with namespace: http://ns.microsoft.com/photo/1.2/
