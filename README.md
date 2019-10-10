# LabelObj

# Installation

Download the file from [Github](https://github.com/2020-Computer-Vision/LabelObj/releases)

or install from source

# Usage

1. create a `config.ini` for custom labels

```
label_1
label_2
label_3
```

if need a persistent label `something`, add a line
```
something,1
```

2. Open a video file

You can load existing label.

3. See the help info messsage box

| Key Shortcut | Description |
|--------------|-------------|
| W | Enter "New label" mode with last selected class |
| 1-9 | Enter "New label" mode with class id |
| ESC | Cancel "New label" mode |
| D | Next frame |
| A | Previous frame |
| Delete | Remove all selected labels |
| Ctrl+C/V | Copy and paste selected labels |

4. Do the labeling

Persistent label only will be add when you seek next frame and a new persistent label was added in the current frame. When you seek to non-next frame, the persistent label histroy will be cleared.

5. Save the file

There is a auto save feature, it will autosave the label file to the same folder as the program.

## Label file fromat

```
// here is comment
<frame> <label name> <center x> <center y> <width> <height>
```

Center coordinate and the width and height are the ratio to the width and height of the frame.
