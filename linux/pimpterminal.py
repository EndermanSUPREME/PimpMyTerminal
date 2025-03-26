from PIL import Image
import argparse
import random
import os

# Path to the QTerminal configuration file
QTERMINAL_CONFIG = "~/.config/qterminal.org/qterminal.ini"

OK = "[" + "\033[32m" + "+" + "\033[0m" + "]"
ERR = "[" + "\033[31m" + "-" + "\033[0m" + "]"
INFO = "[" + "\033[33m" + "*" + "\033[0m" + "]"

# take a normal png image and apply alpha to it before pushing it to terminal
def create_bg_image(image_path: str, alpha: int, terminal_name:str):
    try:
        print(f"{INFO} Creating new background image with alpha. . .")
        # extract the directory where the source image is coming from
        # to use it to store the output image
        output_path = os.path.dirname(image_path) + "/" + terminal_name + "_bgimage.png"

        # Open the image
        img = Image.open(image_path).convert("RGBA")

        # Split channels
        r, g, b, a = img.split()

        # Apply the new alpha value
        new_alpha = a.point(lambda p: min(p, alpha))

        # Merge channels back
        img.putalpha(new_alpha)

        # Save the image
        img.save(output_path, format="PNG")

        print(f"{OK} Image Generated Successfully! {output_path}")
        return output_path
    except Exception as e:
        print(f"{ERR} Error: {e}")
        return image_path

def find_valid_images(imgCollectionPath:str, terminal_name:str):
    valid_images = []
    allowed_extensions = {".png", ".jpg", ".jpeg"}

    if not os.path.isdir(imgCollectionPath):
        print(f"Error: '{imgCollectionPath}' is not a valid imgCollectionPath.")
        return []

    for filename in os.listdir(imgCollectionPath):
        filepath = os.path.join(imgCollectionPath, filename)

        # Check if it's a file and has a valid extension
        if os.path.isfile(filepath) and any(filename.lower().endswith(ext) for ext in allowed_extensions):
            try:
                if str(terminal_name+"_bgimage") not in filepath:
                    with Image.open(filepath) as img:
                        img.verify()  # Verify if it's a valid image
                    valid_images.append(filepath)
            except Exception as e:
                print(f"Skipping invalid image: {filename} ({e})")

    return valid_images

def change_qterminal_background(imagePath:str, imageAlpha:int):
    terminal_name = "qterminal"
    if os.path.isabs(imagePath):
        print(f"{INFO} Searching for user qterminal config file. . .")
        print(f"{INFO} CONF-FILE: {QTERMINAL_CONFIG}")

        keys = []

        # resolve the ~ to the corresponding user's home imgCollectionPath
        FULL_QTERMINAL_CONFIG_PATH = os.path.expanduser(QTERMINAL_CONFIG)
        if os.path.isfile(FULL_QTERMINAL_CONFIG_PATH):
            print(f"{OK} Located user qterminal config file!")
            # Load the configuration file as a list of strings
            with open(FULL_QTERMINAL_CONFIG_PATH, "r") as cf:
                keys = cf.readlines()
        else:
            print(f"{ERR} Could not locate user qterminal config file!")
            return

        # Determine if we are using a file or fetching a random file
        if not os.path.isfile(imagePath): # image collection imgCollectionPath given
            print(f"{INFO} Pulling random image from path. . .")
            # get all valid image files from given path
            valid_imgs = find_valid_images(imagePath, terminal_name)

            # generate bg image from a random image from this collection
            imagePath = create_bg_image(random.choice(valid_imgs), imageAlpha, terminal_name)
        else:
            # generate bg image from desired image
            imagePath = create_bg_image(imagePath, imageAlpha, terminal_name)

        # iterate through the collect config file keys and modify whats needed
        for i in range(0,len(keys)):
            if "TerminalBackgroundImage" in keys[i]:
                # changing the attribute like this means we need to
                # reapply the newline before writing to the file
                keys[i] = "TerminalBackgroundImage=" + imagePath + "\n"
            if "TerminalBackgroundMode" in keys[i]:
                # ensures the image is stretched across the entire terminal
                keys[i] = "TerminalBackgroundMode=1\n"

        # Save the changes to the config file
        with open(FULL_QTERMINAL_CONFIG_PATH, "w") as cf:
            for key in keys:
                cf.write(key)

        print(f"{OK} Background color updated!")
        print(f"{INFO} Restart Terminal to Apply Changes!")

    else:
        print(f"{ERR} {imagePath} is not an absolute path!")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("-t", "--target", help="background image absolute path [supply directory for random image]", required=True)
    parser.add_argument("-a","--alpha", type=int, help="background image alpha value [0-255]", default=127)
    parser.add_argument("-n","--name", help="name of terminal being customized [qterminal,gnome,xfc,mate]", required=True)

    args = parser.parse_args()
    if args.name == "qterminal":
        change_qterminal_background(args.target, args.alpha)
    else:
        print(f"{INFO} Work in Progress. . .")

if __name__ == "__main__":
    main()
