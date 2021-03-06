﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Pen rojo = new Pen(Brushes.Red, 6);
        private readonly Brush puntos_rojos =  new SolidColorBrush(Color.FromArgb(255, 255, 218, 185));
 
        private readonly Pen amarillo = new Pen(Brushes.Yellow, 6);
        private readonly Brush puntos_amarillos = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        private readonly Pen azul = new Pen(Brushes.Cyan, 6);
        private readonly Brush puntos_azules =  new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        private readonly Pen verde = new Pen(Brushes.Green, 6);
        private readonly Brush puntos_verdes =  new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 255, 218, 185));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private Pen trackedBonePen = new Pen(Brushes.Red, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's FReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                            this.position15(skel,dc);
                            
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints. En nuestro caso todos los huesos no afectados
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            //this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            //this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            //this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            //this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            //this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            //this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            //this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            //this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            //this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        /// <param name="col"> segun este valor se pintaran el hueso de un color determinado</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1, int col=0)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            //Modificamos esta función para que pinte los colores adecuadamente. Azul si te has pasado de la posición, amarillo si nos has llegado, verde si es correcta y rojo si te has pasado
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked && col==0)
            {
                drawPen = this.trackedBonePen;
                this.trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 255, 218, 185));
                drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
            }

            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked && col == 1)
            {
                drawPen = new Pen(Brushes.Green, 6);
                this.trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
                drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
            }

            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked && col == 2)
            {
                drawPen = new Pen(Brushes.Yellow, 6);
                this.trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
                drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
            }

            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked && col == 3)
            {
                drawPen = new Pen(Brushes.Blue, 6);
                this.trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
                drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));

            }

        }

        private void dibuja_cruz(Skeleton skeleton, DrawingContext drawingContext, int col=0)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft,col);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight,col);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft,col);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft,col);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft,col);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight,col);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight,col);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight,col);
        }

        private void dibuja_pierna_mov(Skeleton skeleton, DrawingContext drawingContext, int col = 0)
        {
            // Render Torso
            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft,col);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft,col);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft,col);
        }



        /// <summary>
        /// Detecta posición 15 al completo y pinta de rojo y verde segun se haya alcanzado la posición o no
        /// </summary>
        /// <param name="skeleton"> skeleton acual. Nube de puntos</param>
        private void position15(Skeleton skeleton, DrawingContext drawingContext)
        {
            //Pintamos los puntos de brazos en cruz segun su posición
            //Rojo
            if (this.cruz(skeleton)==0)
            {
                dibuja_cruz(skeleton, drawingContext);
            }
            //Verde
            if (this.cruz(skeleton) == 1)
            {
                dibuja_cruz(skeleton, drawingContext,1);
            }
            //Amarillo
            if (this.cruz(skeleton) == 2)
            {
                dibuja_cruz(skeleton, drawingContext,2);
            }
            //Azul
            if (this.cruz(skeleton) == 3)
            {
                dibuja_cruz(skeleton, drawingContext,3);
            }

            //Pintamos la pierna segun su posición
            //Rojo
            if (this.pierna_izq_arriba(skeleton)==0)
            {
                dibuja_pierna_mov(skeleton, drawingContext);
            }
            //Verde
            if (this.pierna_izq_arriba(skeleton) == 1)
            {
                dibuja_pierna_mov(skeleton, drawingContext,1);
            }
            //Amarillo
            if (this.pierna_izq_arriba(skeleton) == 2)
            {
                dibuja_pierna_mov(skeleton, drawingContext,2);
            }
            //Azul
            if (this.pierna_izq_arriba(skeleton) == 3)
            {
                dibuja_pierna_mov(skeleton, drawingContext,3);
            }


        }

        /// <summary>
        /// Detecta posición en cruz
        /// </summary>
        /// <param name="skeleton"> skeleton acual. Nube de puntos</param>
        /// <return> entero . -1 si el angulo no es valido. 0 si la posición no es correcta. 1 si la posición es correcta. 2 si esta por debajo. 3. si esta por arriba</return>
        private int cruz (Skeleton skeleton)
        {
            //Guardamos las posiciones a comparar
            //Parte izquierda del brazo
            float pos_y_hombro_left=skeleton.Joints[JointType.ShoulderLeft].Position.Y;
            float pos_y_elbow_left=skeleton.Joints[JointType.ElbowLeft].Position.Y;
            float pos_y_wrist_left=skeleton.Joints[JointType.WristLeft].Position.Y;
            
            //Parte derecha del brazo
            float pos_y_hombro_right = skeleton.Joints[JointType.ShoulderRight].Position.Y;
            float pos_y_elbow_right = skeleton.Joints[JointType.ElbowRight].Position.Y;
            float pos_y_wrist_right = skeleton.Joints[JointType.WristRight].Position.Y;

            //Brazo estirado
             
            if ( 
                !((((pos_y_hombro_left * 1.05) <=  pos_y_elbow_left) || ((pos_y_hombro_left  * 0.95) >= pos_y_elbow_left)) 
                &&
                (((pos_y_hombro_left * 1.05) <= pos_y_wrist_left) || ((pos_y_hombro_left * 0.95) >= pos_y_wrist_left))
                &&
                (((pos_y_hombro_right * 1.05) <= pos_y_elbow_right) || ((pos_y_hombro_right * 0.95) >= pos_y_elbow_right))
                &&
                (((pos_y_hombro_right * 1.05) <= pos_y_wrist_right) || ((pos_y_hombro_right * 0.95) >= pos_y_wrist_right)))
                )
            {
                return 1;    
            }
            else
            {
                //Brazo por encima
                if (!((((pos_y_hombro_left * 1.05) >= pos_y_elbow_left))
                &&
                (((pos_y_hombro_left * 1.05) >= pos_y_wrist_left))
                &&
                (((pos_y_hombro_right * 1.05) >= pos_y_elbow_right))
                &&
                (((pos_y_hombro_right * 1.05) >= pos_y_wrist_right))
                ))
                {
                    return 3;
                }
                else
                {
                    //Brazo por debajo
                    if (!((((pos_y_hombro_left * 1.05) <= pos_y_elbow_left))
                       &&
                       (((pos_y_hombro_left * 1.05) <= pos_y_wrist_left))
                       &&
                       (((pos_y_hombro_right * 1.05) <= pos_y_elbow_right))
                       &&
                       (((pos_y_hombro_right * 1.05) <= pos_y_wrist_right))
                       ))
                    {
                        return 2;
                    }
                        //Incorrecto
                    else
                    {
                        return 0;
                    }
                }
            }
         }

        /// <summary>
        /// Detecta posición en cruz
        /// </summary>
        /// <param name="skeleton"> skeleton acual. Nube de puntos</param>
        /// <param name="angulo"> Angulo deseado alcanzar al mover el pie. Consideramos que este angulo no va a poder pasar de 90 grados. Si no se especifica un angulo presupongo angulo de 20 grados</param>
        /// <return> entero . -1 si el angulo no es valido. 0 si la posición no es correcta. 1 si la posición es correcta. 2 si esta por debajo. 3. si esta por arriba</return>
        private int pierna_izq_arriba(Skeleton skeleton, double angulo = 10.0)
        {
            //Color
            int num = -1;
             
            //Si el angulo propocionado esta dentro de lo razonable 
            if (angulo >= 0 && angulo <= 90)
            {
                //Alamcenamos la posición de las rodillas y tobillos izquierdos 
                Joint rodilla_izquierda = skeleton.Joints[JointType.KneeLeft];
                SkeletonPoint pos_rodilla_izquierda = rodilla_izquierda.Position;
                Joint tobillo_izquierdo = skeleton.Joints[JointType.AnkleLeft];
                SkeletonPoint pos_tobillo_izquierdo = tobillo_izquierdo.Position;
                //Alamcenamos la posición de las rodillas y tobillos derechos
                Joint  rodilla_derecha= skeleton.Joints[JointType.KneeRight];
                SkeletonPoint pos_rodilla_derecha = rodilla_derecha.Position;
                Joint tobillo_derecho = skeleton.Joints[JointType.AnkleRight];
                SkeletonPoint pos_tobillo_derecho = tobillo_derecho.Position;
                
                //Calculo de catetos del triangulo

                double cateto_a = dist_ecuclidea(pos_tobillo_izquierdo.Z, pos_tobillo_izquierdo.Z, pos_rodilla_izquierda.Y, pos_tobillo_izquierdo.Y);           // lado adyacente
                double cateto_b = dist_ecuclidea(pos_tobillo_izquierdo.Z, pos_rodilla_izquierda.Z, pos_rodilla_izquierda.Y, pos_rodilla_izquierda.Y);           // lado opuesto

                //Calculo de angulo a comparar
                double angulo_actual =  conversor_rad_a_grados(Math.Atan((cateto_a/cateto_b)));
               
                if (angulo_actual == 0)
                {
                    num = 0;
                }

               //Por encima
               if ((angulo_actual <= angulo * 0.95) && angulo_actual != 0)
               {
                   num = 3;
               }
               else
               {
                   //Por debajo
                   if (angulo_actual >= angulo * 1.05)
                   {
                       num = 2;
                   }
                   else
                   {
                       //Posición correcta
                       if ((angulo_actual >= angulo * 1.05) && (angulo_actual <= angulo * 0.95))
                       {
                           num = 1;
                       }
                       else
                       {
                           num = 0;
                       }
                    }
                }
            }
            return num; 
        }

        /// <summary>
        /// Conversor de grados a radianes
        /// </summary>
        /// <param name="angulo"> angulo en radianes a convertir a grados</param>
        private double conversor_rad_a_grados(double angulo)
        {
            return ((angulo * 180) / Math.PI);
        }


        /// <summary>
        /// Distancia euclidea entre dos puntos
        /// </summary>
        /// <param name="p1_x"></param>
        /// <param name="p2_x"></param>
        /// <param name="p1_y"></param>
        /// <param name="p2_y"></param>
        /// <returns> Distancia entre dos puntos double</returns>
        private static double dist_ecuclidea(double p1_x, double p2_x, double p1_y, double p2_y)
        {
            return Math.Sqrt(Math.Pow((p2_x-p1_x),2)+(Math.Pow((p2_y-p1_y),2)));
        }



    }
}