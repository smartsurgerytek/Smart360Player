﻿using OVR.OpenVR;

public interface IEditionModel
{
     public Edition[] data { get; internal set; }
}
public interface IModel
{
    void Initialize();
}
