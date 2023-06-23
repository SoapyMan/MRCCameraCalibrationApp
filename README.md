# MRCCameraCalibrationApp
This is a reverse-engineered and improved version of Oculus MRC Camera Calibration App (com.oculus.MrcCameraCalibration)

It is aimed to be compatible both with [Oculus PC MRC app](https://developer.oculus.com/downloads/package/mixed-reality-capture-tools/) and [Reality Mixer](https://github.com/fabio914/RealityMixer) and maybe other Mixed Reality applications.

# WORK IN PROGRESS

What is done:
- Ported on newer Unity Engine (2022.3.2f1)
- Added controllers display
- Added passthrough mode for convinience (works best on Quest Pro and upcoming Quest 3)
- Fixed few issues with failing `mrc.xml` saving

Current Issues:
- Discovery is not working for PC MRC app
- `mrc.xml` is not saved in every game folder, scoped permissions work as long as you provide access to specific package folder

Future:
- Some UI for managing the games permissions we want to calibrate for
