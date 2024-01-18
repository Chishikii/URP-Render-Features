# URP Render Features

Various custom render features for unity 2023.2+.
These features are written with URP Version 16.0.4 and include RendererLists and the Blitter API.

A big help was [this](https://www.cyanilux.com/tutorials/custom-renderer-features/) blog post by Cyanilux.

Something you need to be aware of if you're coming from older version is that the blitting has changed to using a
fullscreen shader.
This shader can get the "_BlitSource" texture directly via a "URP Sample Buffer" node.

I've made textures available by setting them as global textures in the command buffer but you could also pass them to the
shader directly via ids.

## Outline Render Feature

The outline render feature is taken from [Robinseibold](https://github.com/Robinseibold/Unity-URP-Outlines/)
implementation of [Erik Roystan Ross Outline Shader](https://roystan.net/articles/outline-shader.html). 
I've made some minor adjustments to make it compatible with newer urp versions.
You can control which objects receive outlines by specifying LayerMasks and RenderLayers.

## Desaturation Render Feature

Similar to the outlines this feature can be controlled by using LayerMasks and RenderLayers.

## Blur Render Feature

This was take and adapted from Unity's documentation
tutorial [here](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/containers/create-custom-renderer-feature-1.html).
